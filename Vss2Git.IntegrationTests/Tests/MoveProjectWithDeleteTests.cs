using System.Linq;
using FluentAssertions;
using Hpdi.Vss2Git.IntegrationTests.Helpers;

namespace Hpdi.Vss2Git.IntegrationTests.Tests;

/// <summary>
/// Integration tests for Scenario13_MoveProjectWithDelete.
/// Verifies that when a subproject is moved into a project and root-level
/// files are deleted in the same changeset, no stale files remain on disk.
/// </summary>
public class MoveProjectWithDeleteTests : IDisposable
{
    private readonly MigrationTestRunner _runner = new();

    public MoveProjectWithDeleteTests()
    {
        _runner.Run("13_MoveProjectWithDelete");
    }

    [Fact]
    public void Migration_MovedFilesExist()
    {
        var inspector = _runner.Inspector!;

        // Code subproject should be under Dest with its files
        inspector.DirectoryExists("App/Dest/Code").Should().BeTrue(
            "Code subproject was moved into Dest");
        inspector.FileExists("App/Dest/Code/main.pas").Should().BeTrue();
        inspector.FileExists("App/Dest/Code/helper.pas").Should().BeTrue();
    }

    [Fact]
    public void Migration_MovedFileContent()
    {
        var content = _runner.Inspector!.GetFileContent("App/Dest/Code/main.pas");
        content.Should().Contain("after move into Dest");
    }

    [Fact]
    public void Migration_DeletedFilesNotInTree()
    {
        var inspector = _runner.Inspector!;

        // Root-level files should NOT be in the git tree
        inspector.FileExists("App/Dest/main.pas").Should().BeFalse(
            "root-level main.pas was deleted");
        inspector.FileExists("App/Dest/config.dfm").Should().BeFalse(
            "root-level config.dfm was deleted");
        inspector.FileExists("App/Dest/utils.pas").Should().BeFalse(
            "root-level utils.pas was deleted");
    }

    [Fact]
    public void Migration_SourceProjectEmpty()
    {
        _runner.Inspector!.DirectoryExists("App/Src/Code").Should().BeFalse(
            "Code was moved out of Src");
    }

    [Fact]
    public void Migration_NoStaleFilesOnDisk()
    {
        var inspector = _runner.Inspector!;

        // This is the key assertion: after migration the working directory
        // should match HEAD exactly with no leftover files.
        inspector.HasCleanWorkingDirectory().Should().BeTrue(
            "migration should leave a clean working directory with no stale files");
    }

    [Fact]
    public void Migration_NoUntrackedFiles()
    {
        var untracked = _runner.Inspector!.GetUntrackedFiles();

        untracked.Should().BeEmpty(
            "no stale files should remain on disk after move + delete");
    }

    public void Dispose() => _runner.Dispose();
}
