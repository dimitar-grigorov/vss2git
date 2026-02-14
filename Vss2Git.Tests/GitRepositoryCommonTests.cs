using System.Text;
using FluentAssertions;
using LibGit2Sharp;
using Xunit;

namespace Hpdi.Vss2Git.Tests
{
    /// <summary>
    /// Shared tests that every IGitRepository backend must pass.
    /// Derived classes provide the backend-specific factory method.
    /// Inspection always happens after Dispose() since FastImport
    /// requires finalization before objects are readable.
    /// </summary>
    public abstract class GitRepositoryCommonTests : IDisposable
    {
        private readonly string _repoDir;
        private readonly IGitRepository _repo;
        private bool _disposed;

        protected GitRepositoryCommonTests()
        {
            _repoDir = Path.Combine(Path.GetTempPath(),
                "vss2git_common_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(_repoDir);
            _repo = CreateRepository(_repoDir);
            _repo.Init();
        }

        /// <summary>Factory method — each backend provides its own implementation.</summary>
        private protected abstract IGitRepository CreateRepository(string repoPath);

        public void Dispose()
        {
            FinalizeRepo();
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

        private void FinalizeRepo()
        {
            if (!_disposed)
            {
                _repo.Dispose();
                _disposed = true;
            }
        }

        private string CreateFile(string relativePath, string content = "test content")
        {
            var fullPath = Path.Combine(_repoDir, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, content);
            return fullPath;
        }

        private List<string> GetHeadFileList()
        {
            FinalizeRepo();
            using var gitRepo = new Repository(_repoDir);
            if (gitRepo.Head.Tip == null)
                return new List<string>();
            return CollectPaths(gitRepo.Head.Tip.Tree, "").ToList();
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

        private byte[] GetHeadFileContent(string relativePath)
        {
            FinalizeRepo();
            using var gitRepo = new Repository(_repoDir);
            var blob = gitRepo.Head.Tip[relativePath].Target as Blob;
            using var stream = blob!.GetContentStream();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }

        #region Add and Commit

        [Fact]
        public void AddAll_WithPaths_CommitsFiles()
        {
            var f1 = CreateFile("file1.txt", "hello");
            var f2 = CreateFile("sub/file2.txt", "world");
            _repo.AddAll(new[] { f1, f2 });
            _repo.Commit("test", "test@test", "initial commit", DateTime.Now);

            var files = GetHeadFileList();
            files.Should().Contain("file1.txt");
            files.Should().Contain("sub/file2.txt");
        }

        [Fact]
        public void AddAll_WithPaths_OnlyStagesSpecifiedFiles()
        {
            var wanted = CreateFile("wanted.txt", "yes");
            var unwanted = CreateFile("unwanted.txt", "no");
            _repo.AddAll(new[] { wanted });
            _repo.Commit("test", "test@test", "initial", DateTime.Now);

            var files = GetHeadFileList();
            files.Should().Contain("wanted.txt");
            files.Should().NotContain("unwanted.txt",
                "AddAll(changedPaths) should only stage specified files");
        }

        [Fact]
        public void Commit_WithNoOperations_ReturnsFalse()
        {
            var result = _repo.Commit("test", "test@test", "empty", DateTime.Now);
            result.Should().BeFalse("no files were staged");
        }

        [Fact]
        public void BinaryFileContent_PreservedCorrectly()
        {
            var binaryContent = new byte[256];
            for (int i = 0; i < 256; i++)
                binaryContent[i] = (byte)i;

            var fullPath = Path.Combine(_repoDir, "binary.dat");
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllBytes(fullPath, binaryContent);
            _repo.AddAll(new[] { fullPath });
            _repo.Commit("test", "test@test", "add binary", DateTime.Now);

            var actual = GetHeadFileContent("binary.dat");
            actual.Should().Equal(binaryContent, "binary content must be preserved byte-for-byte");
        }

        [Fact]
        public void AddAll_WithPaths_HandlesDeletedFiles()
        {
            var f1 = CreateFile("keep.txt", "keep");
            var f2 = CreateFile("delete-me.txt", "delete");
            _repo.AddAll(new[] { f1, f2 });
            _repo.Commit("test", "test@test", "initial", DateTime.Now);

            File.Delete(f2);
            _repo.AddAll(new[] { f2 });
            _repo.Commit("test", "test@test", "delete file", DateTime.Now);

            var files = GetHeadFileList();
            files.Should().Contain("keep.txt");
            files.Should().NotContain("delete-me.txt", "deleted file should be removed");
        }

        [Fact]
        public void MultipleCommits_MaintainLinearHistory()
        {
            var f1 = CreateFile("file1.txt", "v1");
            _repo.AddAll(new[] { f1 });
            _repo.Commit("user1", "u1@test", "first commit", DateTime.Now);

            File.WriteAllText(f1, "v2");
            _repo.AddAll(new[] { f1 });
            _repo.Commit("user2", "u2@test", "second commit", DateTime.Now);

            var f2 = CreateFile("file2.txt", "new file");
            _repo.AddAll(new[] { f2 });
            _repo.Commit("user1", "u1@test", "third commit", DateTime.Now);

            FinalizeRepo();
            using var gitRepo = new Repository(_repoDir);
            var filter = new CommitFilter
            {
                SortBy = CommitSortStrategies.Topological
            };
            var commits = gitRepo.Commits.QueryBy(filter)
                .Select(c => new { c.Message, AuthorName = c.Author.Name, ParentCount = c.Parents.Count() })
                .ToList();

            commits.Should().HaveCount(3);
            commits[0].Message.Should().Contain("third commit");
            commits[1].Message.Should().Contain("second commit");
            commits[2].Message.Should().Contain("first commit");

            commits[0].ParentCount.Should().Be(1);
            commits[1].ParentCount.Should().Be(1);
            commits[2].ParentCount.Should().Be(0, "first commit is root");
        }

        [Fact]
        public void Commit_WithNonAsciiMessage_PreservesUnicode()
        {
            var f1 = CreateFile("test.txt", "content");
            _repo.AddAll(new[] { f1 });
            _repo.Commit("René Müller", "rene@test.com", "Ändere Datei für Qualität", DateTime.Now);

            FinalizeRepo();
            using var gitRepo = new Repository(_repoDir);
            var commit = gitRepo.Head.Tip;
            commit.Message.Should().Contain("Ändere Datei für Qualität");
            commit.Author.Name.Should().Be("René Müller");
        }

        #endregion

        #region Remove

        [Fact]
        public void Remove_Recursive_AfterCommit_RemovesDirectory()
        {
            var f1 = CreateFile("proj/file1.txt", "hello1");
            var f2 = CreateFile("proj/sub/file2.txt", "hello2");
            _repo.AddAll(new[] { f1, f2 });
            _repo.Commit("test", "test@test", "initial", DateTime.Now);

            _repo.Remove(Path.Combine(_repoDir, "proj"), recursive: true);
            _repo.Commit("test", "test@test", "delete proj", DateTime.Now);

            var files = GetHeadFileList();
            files.Should().BeEmpty("all files were in proj which was deleted");
        }

        [Fact]
        public void Remove_Recursive_BeforeCommit_RemovesDirectory()
        {
            var f1 = CreateFile("proj/file1.txt", "hello1");
            var f2 = CreateFile("proj/sub/file2.txt", "hello2");
            var other = CreateFile("other.txt", "keep me");
            _repo.AddAll(new[] { f1, f2, other });

            _repo.Remove(Path.Combine(_repoDir, "proj"), recursive: true);
            _repo.Commit("test", "test@test", "add and delete", DateTime.Now);

            var files = GetHeadFileList();
            files.Should().NotContain(f => f.StartsWith("proj"),
                "deleted directory should not appear in commit");
            files.Should().Contain("other.txt", "unrelated file should survive");
        }

        [Fact]
        public void Remove_Recursive_MixedState_RemovesDirectory()
        {
            var f1 = CreateFile("proj/file1.txt", "hello1");
            _repo.AddAll(new[] { f1 });
            _repo.Commit("test", "test@test", "initial", DateTime.Now);

            var f2 = CreateFile("proj/file2.txt", "hello2");
            _repo.AddAll(new[] { f2 });

            _repo.Remove(Path.Combine(_repoDir, "proj"), recursive: true);
            _repo.Commit("test", "test@test", "delete mixed proj", DateTime.Now);

            var files = GetHeadFileList();
            files.Should().NotContain(f => f.StartsWith("proj"),
                "mixed-state directory should be fully removed");
        }

        [Fact]
        public void Remove_SingleFile_Works()
        {
            var f1 = CreateFile("proj/file1.txt", "hello1");
            var f2 = CreateFile("proj/file2.txt", "hello2");
            _repo.AddAll(new[] { f1, f2 });
            _repo.Commit("test", "test@test", "initial", DateTime.Now);

            _repo.Remove(f1, recursive: false);
            _repo.Commit("test", "test@test", "remove one file", DateTime.Now);

            var files = GetHeadFileList();
            files.Should().NotContain("proj/file1.txt", "deleted file should be gone");
            files.Should().Contain("proj/file2.txt", "other file should remain");
        }

        #endregion

        #region Move

        [Fact]
        public void Move_SingleFile_RenamesCorrectly()
        {
            var f1 = CreateFile("proj/file1.txt", "hello1");
            var f2 = CreateFile("proj/file2.txt", "hello2");
            _repo.AddAll(new[] { f1, f2 });
            _repo.Commit("test", "test@test", "initial", DateTime.Now);

            var dest = Path.Combine(_repoDir, "proj", "renamed.txt");
            _repo.Move(f1, dest);
            _repo.Commit("test", "test@test", "rename file", DateTime.Now);

            var files = GetHeadFileList();
            files.Should().NotContain("proj/file1.txt", "old name should be gone");
            files.Should().Contain("proj/renamed.txt");
            files.Should().Contain("proj/file2.txt", "other file should remain");
        }

        [Fact]
        public void Move_SingleFile_BeforeCommit_Works()
        {
            var f1 = CreateFile("proj/file1.txt", "hello1");
            var f2 = CreateFile("proj/file2.txt", "hello2");
            _repo.AddAll(new[] { f1, f2 });

            var dest = Path.Combine(_repoDir, "proj", "renamed.txt");
            _repo.Move(f1, dest);
            _repo.Commit("test", "test@test", "move file", DateTime.Now);

            var files = GetHeadFileList();
            files.Should().NotContain("proj/file1.txt", "old file path should be gone");
            files.Should().Contain("proj/renamed.txt");
            files.Should().Contain("proj/file2.txt", "other file should remain");
        }

        [Fact]
        public void Move_Directory_AfterCommit_MovesSubtree()
        {
            var f1 = CreateFile("olddir/file1.txt", "hello1");
            var f2 = CreateFile("olddir/sub/file2.txt", "hello2");
            _repo.AddAll(new[] { f1, f2 });
            _repo.Commit("test", "test@test", "initial", DateTime.Now);

            _repo.Move(Path.Combine(_repoDir, "olddir"), Path.Combine(_repoDir, "newdir"));
            _repo.Commit("test", "test@test", "move dir", DateTime.Now);

            var files = GetHeadFileList();
            files.Should().NotContain(f => f.StartsWith("olddir"), "old path should be gone");
            files.Should().Contain("newdir/file1.txt");
            files.Should().Contain("newdir/sub/file2.txt");
        }

        [Fact]
        public void Move_Directory_BeforeCommit_MovesSubtree()
        {
            var f1 = CreateFile("olddir/file1.txt", "hello1");
            var f2 = CreateFile("olddir/sub/file2.txt", "hello2");
            var other = CreateFile("other.txt", "keep me");
            _repo.AddAll(new[] { f1, f2, other });

            _repo.Move(Path.Combine(_repoDir, "olddir"), Path.Combine(_repoDir, "newdir"));
            _repo.Commit("test", "test@test", "add and move", DateTime.Now);

            var files = GetHeadFileList();
            files.Should().NotContain(f => f.StartsWith("olddir"), "old path should be gone");
            files.Should().Contain("newdir/file1.txt");
            files.Should().Contain("newdir/sub/file2.txt");
            files.Should().Contain("other.txt", "unrelated file should survive");
        }

        [Fact]
        public void Move_Directory_MixedState_MovesSubtree()
        {
            var f1 = CreateFile("olddir/file1.txt", "hello1");
            _repo.AddAll(new[] { f1 });
            _repo.Commit("test", "test@test", "initial", DateTime.Now);

            var f2 = CreateFile("olddir/file2.txt", "hello2");
            _repo.AddAll(new[] { f2 });

            _repo.Move(Path.Combine(_repoDir, "olddir"), Path.Combine(_repoDir, "newdir"));
            _repo.Commit("test", "test@test", "move mixed dir", DateTime.Now);

            var files = GetHeadFileList();
            files.Should().NotContain(f => f.StartsWith("olddir"), "old path should be gone");
            files.Should().Contain("newdir/file1.txt");
            files.Should().Contain("newdir/file2.txt");
        }

        #endregion

        #region Tags

        [Fact]
        public void Tag_CreatesAnnotatedTag()
        {
            var f1 = CreateFile("test.txt", "content");
            _repo.AddAll(new[] { f1 });
            _repo.Commit("test", "test@test", "initial", DateTime.Now);

            _repo.Tag("v1.0", "tagger", "tagger@test", "Release 1.0", DateTime.Now);

            FinalizeRepo();
            string tagMessage;
            string taggerName;
            bool isAnnotated;
            using (var gitRepo = new Repository(_repoDir))
            {
                var tag = gitRepo.Tags["v1.0"];
                tag.Should().NotBeNull();
                isAnnotated = tag.IsAnnotated;
                tagMessage = tag.Annotation.Message;
                taggerName = tag.Annotation.Tagger.Name;
            }
            isAnnotated.Should().BeTrue();
            tagMessage.Should().Contain("Release 1.0");
            taggerName.Should().Be("tagger");
        }

        #endregion
    }

    // Concrete test classes — one per backend

    public class GitWrapperCommonTests : GitRepositoryCommonTests
    {
        private protected override IGitRepository CreateRepository(string repoPath)
            => new GitWrapper(repoPath, Logger.Null);
    }

    public class LibGit2SharpCommonTests : GitRepositoryCommonTests
    {
        private protected override IGitRepository CreateRepository(string repoPath)
            => new LibGit2SharpRepository(repoPath, Logger.Null);
    }

    public class FastImportCommonTests : GitRepositoryCommonTests
    {
        private protected override IGitRepository CreateRepository(string repoPath)
            => new FastImportRepository(repoPath, Logger.Null);
    }
}
