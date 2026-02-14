using FluentAssertions;
using LibGit2Sharp;
using Xunit;

namespace Hpdi.Vss2Git.Tests
{
    /// <summary>
    /// Tests for LibGit2SharpRepository focusing on TreeDefinition edge cases.
    ///
    /// LibGit2Sharp's TreeDefinition behaves differently for uncommitted vs committed
    /// subtrees: Remove("dir") silently fails and Add("dir2", tree["dir"]) crashes
    /// when the subtree was built incrementally without CreateTree(). These tests
    /// verify the workarounds in LibGit2SharpRepository.
    /// </summary>
    public class LibGit2SharpRepositoryTests : IDisposable
    {
        private readonly string _repoDir;
        private readonly LibGit2SharpRepository _repo;

        public LibGit2SharpRepositoryTests()
        {
            _repoDir = Path.Combine(Path.GetTempPath(),
                "vss2git_l2s_test_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(_repoDir);
            _repo = new LibGit2SharpRepository(_repoDir, Logger.Null);
            _repo.Init();
        }

        public void Dispose()
        {
            _repo.Dispose();
            try
            {
                if (Directory.Exists(_repoDir))
                {
                    foreach (var f in Directory.EnumerateFiles(_repoDir, "*", SearchOption.AllDirectories))
                        File.SetAttributes(f, FileAttributes.Normal);
                    Directory.Delete(_repoDir, true);
                }
            }
            catch { }
        }

        private string CreateFile(string relativePath, string content = "test content")
        {
            var fullPath = Path.Combine(_repoDir, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, content);
            return fullPath;
        }

        /// <summary>
        /// Reads the HEAD tree entry names. Must keep Repository open during access
        /// because Tree holds a native handle that is freed on dispose.
        /// </summary>
        private List<string> GetHeadFileList()
        {
            using var gitRepo = new Repository(_repoDir);
            var tree = gitRepo.Head.Tip.Tree;
            return CollectPaths(tree, "").ToList();
        }

        private static IEnumerable<string> CollectPaths(Tree tree, string prefix)
        {
            foreach (var entry in tree)
            {
                var path = string.IsNullOrEmpty(prefix) ? entry.Name : prefix + "/" + entry.Name;
                if (entry.TargetType == TreeEntryTargetType.Tree)
                {
                    foreach (var sub in CollectPaths((Tree)entry.Target, path))
                        yield return sub;
                }
                else
                {
                    yield return path;
                }
            }
        }

        #region L1: Remove on uncommitted subtrees

        [Fact]
        public void Remove_Recursive_AfterCommit_RemovesDirectory()
        {
            // Arrange: add files, commit (subtree becomes committed in ODB)
            var f1 = CreateFile("proj/file1.txt", "hello1");
            var f2 = CreateFile("proj/sub/file2.txt", "hello2");
            _repo.AddAll(new[] { f1, f2 });
            _repo.Commit("test", "test@test", "initial", DateTime.Now);

            // Act: remove in next changeset (committed subtree path)
            _repo.Remove(Path.Combine(_repoDir, "proj"), recursive: true);
            _repo.Commit("test", "test@test", "delete proj", DateTime.Now);

            // Assert
            var files = GetHeadFileList();
            files.Should().BeEmpty("all files were in proj which was deleted");
        }

        [Fact]
        public void Remove_Recursive_BeforeCommit_RemovesDirectory()
        {
            // Arrange: add files WITHOUT committing first (uncommitted subtree)
            var f1 = CreateFile("proj/file1.txt", "hello1");
            var f2 = CreateFile("proj/sub/file2.txt", "hello2");
            var other = CreateFile("other.txt", "keep me");
            _repo.AddAll(new[] { f1, f2, other });

            // Act: remove proj before it was ever committed (same changeset pattern)
            _repo.Remove(Path.Combine(_repoDir, "proj"), recursive: true);
            _repo.Commit("test", "test@test", "add and delete", DateTime.Now);

            // Assert: proj should NOT appear in the commit
            var files = GetHeadFileList();
            files.Should().NotContain(f => f.StartsWith("proj"),
                "deleted directory should not appear in commit");
            files.Should().Contain("other.txt", "unrelated file should survive");
        }

        [Fact]
        public void Remove_Recursive_MixedState_RemovesDirectory()
        {
            // Arrange: commit some files, then add more to same dir
            var f1 = CreateFile("proj/file1.txt", "hello1");
            _repo.AddAll(new[] { f1 });
            _repo.Commit("test", "test@test", "initial", DateTime.Now);

            // Add new file to same dir (makes subtree mixed: committed + uncommitted)
            var f2 = CreateFile("proj/file2.txt", "hello2");
            _repo.AddAll(new[] { f2 });

            // Act: delete the whole directory
            _repo.Remove(Path.Combine(_repoDir, "proj"), recursive: true);
            _repo.Commit("test", "test@test", "delete mixed proj", DateTime.Now);

            // Assert
            var files = GetHeadFileList();
            files.Should().NotContain(f => f.StartsWith("proj"),
                "mixed-state directory should be fully removed");
        }

        [Fact]
        public void Remove_SingleFile_AlwaysWorks()
        {
            // Arrange: add files without committing
            var f1 = CreateFile("proj/file1.txt", "hello1");
            var f2 = CreateFile("proj/file2.txt", "hello2");
            _repo.AddAll(new[] { f1, f2 });

            // Act: remove single file (not recursive — individual file removal)
            _repo.Remove(f1, recursive: false);
            _repo.Commit("test", "test@test", "remove one file", DateTime.Now);

            // Assert
            var files = GetHeadFileList();
            files.Should().NotContain("proj/file1.txt", "deleted file should be gone");
            files.Should().Contain("proj/file2.txt", "other file should remain");
        }

        #endregion
    }
}
