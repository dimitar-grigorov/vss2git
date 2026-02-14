using FluentAssertions;
using Xunit;

namespace Hpdi.Vss2Git.Tests
{
    /// <summary>
    /// Tests for the .git directory exclusion predicate used in
    /// GitExporter.RemoveEmptyDirectories (C7 bug fix).
    /// </summary>
    public class RemoveEmptyDirectoriesTests : IDisposable
    {
        private readonly string _rootDir;

        public RemoveEmptyDirectoriesTests()
        {
            _rootDir = Path.Combine(Path.GetTempPath(),
                "vss2git_c7_test_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(_rootDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_rootDir, true); } catch { }
        }

        /// <summary>
        /// Replicates the filter predicate from GitExporter.RemoveEmptyDirectories.
        /// </summary>
        private IEnumerable<string> FilterDirectories(string rootPath)
        {
            var gitDir = Path.Combine(rootPath, ".git");
            var gitDirPrefix = gitDir + Path.DirectorySeparatorChar;
            return Directory.GetDirectories(rootPath, "*", SearchOption.AllDirectories)
                .Where(d => d != gitDir && !d.StartsWith(gitDirPrefix));
        }

        [Fact]
        public void Filter_ExcludesGitDir_ButNotGithubDir()
        {
            // Arrange: create .git/, .git/objects/, .github/, src/
            Directory.CreateDirectory(Path.Combine(_rootDir, ".git", "objects"));
            Directory.CreateDirectory(Path.Combine(_rootDir, ".github"));
            Directory.CreateDirectory(Path.Combine(_rootDir, "src"));

            // Act
            var filtered = FilterDirectories(_rootDir).ToList();

            // Assert: .git and .git/objects excluded; .github and src included
            filtered.Should().NotContain(d => d.Contains(".git" + Path.DirectorySeparatorChar) ||
                                               d.EndsWith(".git"));
            filtered.Should().Contain(Path.Combine(_rootDir, ".github"));
            filtered.Should().Contain(Path.Combine(_rootDir, "src"));
        }

        [Fact]
        public void Filter_ExcludesNestedGitDirs()
        {
            // Arrange
            Directory.CreateDirectory(Path.Combine(_rootDir, ".git", "refs", "heads"));
            Directory.CreateDirectory(Path.Combine(_rootDir, "proj"));

            // Act
            var filtered = FilterDirectories(_rootDir).ToList();

            // Assert: all .git/* excluded, proj included
            filtered.Should().NotContain(d => d.Contains(".git"));
            filtered.Should().Contain(Path.Combine(_rootDir, "proj"));
        }
    }
}
