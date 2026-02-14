using FluentAssertions;
using Xunit;

namespace Hpdi.Vss2Git.Tests
{
    /// <summary>
    /// Tests for GitWrapper.MoveFileSystem - the filesystem fallback used when
    /// 'git mv' fails with "bad source" (files exist on disk but are not yet
    /// tracked in the git index).
    ///
    /// Bug fixed: When a directory containing untracked files was moved via
    /// 'git mv', git failed with "fatal: bad source, source=path". This crashed
    /// the entire migration. The fix falls back to filesystem move, and the
    /// subsequent 'git add -A' picks up the changes.
    /// </summary>
    public class MoveFileSystemTests : IDisposable
    {
        private readonly string _testDir;

        public MoveFileSystemTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "vss2git_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, true);
        }

        #region File moves

        [Fact]
        public void MoveFileSystem_File_MovesToDestination()
        {
            // Arrange
            var sourceFile = Path.Combine(_testDir, "source.txt");
            var destFile = Path.Combine(_testDir, "dest.txt");
            File.WriteAllText(sourceFile, "content");

            // Act
            GitWrapper.MoveFileSystem(sourceFile, destFile);

            // Assert
            File.Exists(sourceFile).Should().BeFalse();
            File.Exists(destFile).Should().BeTrue();
            File.ReadAllText(destFile).Should().Be("content");
        }

        [Fact]
        public void MoveFileSystem_File_CreatesDestinationDirectory()
        {
            // Arrange
            var sourceFile = Path.Combine(_testDir, "source.txt");
            var destFile = Path.Combine(_testDir, "subdir", "dest.txt");
            File.WriteAllText(sourceFile, "content");

            // Act
            GitWrapper.MoveFileSystem(sourceFile, destFile);

            // Assert
            File.Exists(destFile).Should().BeTrue();
            File.ReadAllText(destFile).Should().Be("content");
        }

        #endregion

        #region Directory moves

        [Fact]
        public void MoveFileSystem_Directory_MovesToDestination()
        {
            // Arrange
            var sourceDir = Path.Combine(_testDir, "srcdir");
            var destDir = Path.Combine(_testDir, "dstdir");
            Directory.CreateDirectory(sourceDir);
            File.WriteAllText(Path.Combine(sourceDir, "file.txt"), "data");

            // Act
            GitWrapper.MoveFileSystem(sourceDir, destDir);

            // Assert
            Directory.Exists(sourceDir).Should().BeFalse();
            Directory.Exists(destDir).Should().BeTrue();
            File.ReadAllText(Path.Combine(destDir, "file.txt")).Should().Be("data");
        }

        [Fact]
        public void MoveFileSystem_Directory_CreatesParentDirectory()
        {
            // Arrange
            var sourceDir = Path.Combine(_testDir, "srcdir");
            var destDir = Path.Combine(_testDir, "parent", "dstdir");
            Directory.CreateDirectory(sourceDir);
            File.WriteAllText(Path.Combine(sourceDir, "file.txt"), "data");

            // Act
            GitWrapper.MoveFileSystem(sourceDir, destDir);

            // Assert
            Directory.Exists(destDir).Should().BeTrue();
            File.ReadAllText(Path.Combine(destDir, "file.txt")).Should().Be("data");
        }

        [Fact]
        public void MoveFileSystem_DirectoryWithSubdirectories_MovesAll()
        {
            // Arrange
            var sourceDir = Path.Combine(_testDir, "srcdir");
            Directory.CreateDirectory(Path.Combine(sourceDir, "sub1", "sub2"));
            File.WriteAllText(Path.Combine(sourceDir, "root.txt"), "root");
            File.WriteAllText(Path.Combine(sourceDir, "sub1", "a.txt"), "a");
            File.WriteAllText(Path.Combine(sourceDir, "sub1", "sub2", "b.txt"), "b");

            var destDir = Path.Combine(_testDir, "dstdir");

            // Act
            GitWrapper.MoveFileSystem(sourceDir, destDir);

            // Assert
            Directory.Exists(sourceDir).Should().BeFalse();
            File.ReadAllText(Path.Combine(destDir, "root.txt")).Should().Be("root");
            File.ReadAllText(Path.Combine(destDir, "sub1", "a.txt")).Should().Be("a");
            File.ReadAllText(Path.Combine(destDir, "sub1", "sub2", "b.txt")).Should().Be("b");
        }

        #endregion

        #region Edge cases

        [Fact]
        public void MoveFileSystem_SourceDoesNotExist_DoesNothing()
        {
            // This is the edge case where source was already moved or doesn't exist
            var sourcePath = Path.Combine(_testDir, "nonexistent");
            var destPath = Path.Combine(_testDir, "dest");

            // Act - should not throw
            GitWrapper.MoveFileSystem(sourcePath, destPath);

            // Assert
            Directory.Exists(destPath).Should().BeFalse();
            File.Exists(destPath).Should().BeFalse();
        }

        [Fact]
        public void MoveFileSystem_DestDirectoryAlreadyExists_NoError()
        {
            // Arrange
            var sourceFile = Path.Combine(_testDir, "existing", "file.txt");
            Directory.CreateDirectory(Path.Combine(_testDir, "existing"));
            File.WriteAllText(sourceFile, "content");

            var destDir = Path.Combine(_testDir, "target");
            Directory.CreateDirectory(destDir); // pre-create dest parent
            var destFile = Path.Combine(destDir, "file.txt");

            // Act
            GitWrapper.MoveFileSystem(sourceFile, destFile);

            // Assert
            File.Exists(destFile).Should().BeTrue();
        }

        #endregion
    }
}
