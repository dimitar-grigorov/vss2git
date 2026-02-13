using FluentAssertions;
using Hpdi.Vss2Git.IntegrationTests.Helpers;

namespace Hpdi.Vss2Git.IntegrationTests.Tests;

/// <summary>
/// Integration tests for Scenario02_RenamesAndMoves.
/// </summary>
public class RenameAndMoveTests : IDisposable
{
    private readonly MigrationTestRunner _runner = new();

    public RenameAndMoveTests()
    {
        _runner.Run("02_RenamesAndMoves");
    }

    [Fact]
    public void Migration_RenamedFileAtNewLocation()
    {
        var inspector = _runner.Inspector!;

        // oldname.txt renamed to newname.txt, FolderA renamed to FolderRenamed
        inspector.FileExists("Project/FolderRenamed/newname.txt").Should().BeTrue();
        inspector.GetFileContent("Project/FolderRenamed/newname.txt")
            .Should().Contain("Edited after rename");
    }

    [Fact]
    public void Migration_CaseOnlyRename()
    {
        _runner.Inspector!.GetFileList().Should().Contain(f =>
            f.Equals("Project/FolderRenamed/casename.txt", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Migration_MovedDirectoryAndContent()
    {
        var inspector = _runner.Inspector!;

        inspector.DirectoryExists("Project/FolderB/SubDir").Should().BeTrue(
            "SubDir should be under FolderB after move");
        inspector.GetFileContent("Project/FolderB/SubDir/nested.txt")
            .Should().Contain("Edited after move");
        inspector.FileExists("Project/FolderB/stay.txt").Should().BeTrue();
    }

    public void Dispose() => _runner.Dispose();
}
