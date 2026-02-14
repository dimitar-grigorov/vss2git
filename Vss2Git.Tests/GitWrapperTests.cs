using FluentAssertions;
using LibGit2Sharp;
using Xunit;

namespace Hpdi.Vss2Git.Tests
{
    public class GitWrapperTests : IDisposable
    {
        private readonly string _repoDir;
        private readonly GitWrapper _wrapper;

        public GitWrapperTests()
        {
            _repoDir = Path.Combine(Path.GetTempPath(),
                "vss2git_gw_test_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(_repoDir);
            _wrapper = new GitWrapper(_repoDir, Logger.Null);
            _wrapper.Init();
        }

        public void Dispose()
        {
            _wrapper.Dispose();
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

        #region C6: AddAll(changedPaths) should only stage specified files

        [Fact]
        public void AddAll_WithPaths_OnlyStagesSpecifiedFiles()
        {
            // Arrange: create two files, only pass one to AddAll
            var wanted = Path.Combine(_repoDir, "wanted.txt");
            var unwanted = Path.Combine(_repoDir, "unwanted.txt");
            File.WriteAllText(wanted, "yes");
            File.WriteAllText(unwanted, "no");

            // Act
            _wrapper.AddAll(new[] { wanted });
            _wrapper.Commit("test", "test@test", "initial", DateTime.Now);

            // Assert: only wanted.txt should be in the commit
            var files = GetHeadFileList();
            files.Should().Contain("wanted.txt");
            files.Should().NotContain("unwanted.txt",
                "AddAll(changedPaths) should only stage specified files, not git add -A");
        }

        [Fact]
        public void AddAll_WithNull_StagesEverything()
        {
            // Arrange
            var f1 = Path.Combine(_repoDir, "file1.txt");
            var f2 = Path.Combine(_repoDir, "file2.txt");
            File.WriteAllText(f1, "hello1");
            File.WriteAllText(f2, "hello2");

            // Act: null should fall back to git add -A
            _wrapper.AddAll(null);
            _wrapper.Commit("test", "test@test", "initial", DateTime.Now);

            // Assert: both files committed
            var files = GetHeadFileList();
            files.Should().Contain("file1.txt");
            files.Should().Contain("file2.txt");
        }

        [Fact]
        public void AddAll_WithPaths_HandlesDeletedFiles()
        {
            // Arrange: commit a file first
            var f1 = Path.Combine(_repoDir, "keep.txt");
            var f2 = Path.Combine(_repoDir, "delete-me.txt");
            File.WriteAllText(f1, "keep");
            File.WriteAllText(f2, "delete");
            _wrapper.AddAll(null);
            _wrapper.Commit("test", "test@test", "initial", DateTime.Now);

            // Delete the file on disk, then pass it to AddAll
            File.Delete(f2);
            _wrapper.AddAll(new[] { f2 });
            _wrapper.Commit("test", "test@test", "delete file", DateTime.Now);

            // Assert: delete-me.txt removed from tree
            var files = GetHeadFileList();
            files.Should().Contain("keep.txt");
            files.Should().NotContain("delete-me.txt", "deleted file should be removed");
        }

        #endregion
    }
}
