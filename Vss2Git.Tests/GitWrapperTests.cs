using FluentAssertions;
using LibGit2Sharp;
using Xunit;

namespace Hpdi.Vss2Git.Tests
{
    /// <summary>
    /// Tests for GitWrapper-specific behavior not covered by GitRepositoryCommonTests.
    /// Common operations (add, remove, move, commit, tag, binary, mixed-state)
    /// are tested in GitWrapperCommonTests via the shared base class.
    /// </summary>
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

        #region AddAll(null) fallback — GitWrapper-specific (FastImport doesn't support no-arg AddAll)

        [Fact]
        public void AddAll_WithNull_StagesEverything()
        {
            var f1 = Path.Combine(_repoDir, "file1.txt");
            var f2 = Path.Combine(_repoDir, "file2.txt");
            File.WriteAllText(f1, "hello1");
            File.WriteAllText(f2, "hello2");

            _wrapper.AddAll(null);
            _wrapper.Commit("test", "test@test", "initial", DateTime.Now);

            var files = GetHeadFileList();
            files.Should().Contain("file1.txt");
            files.Should().Contain("file2.txt");
        }

        #endregion
    }
}
