using System.Linq;
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

        inspector.FileExists("Project/FolderRenamed/newname.txt").Should().BeTrue();
        inspector.GetFileContent("Project/FolderRenamed/newname.txt")
            .Should().Contain("Edited after rename");
    }

    [Fact]
    public void Migration_OldNamesDoNotExist()
    {
        var inspector = _runner.Inspector!;

        inspector.FileExists("Project/FolderA/oldname.txt").Should().BeFalse(
            "renamed to newname.txt");
        inspector.FileExists("Project/FolderRenamed/oldname.txt").Should().BeFalse(
            "renamed to newname.txt");
        inspector.DirectoryExists("Project/FolderA").Should().BeFalse(
            "renamed to FolderRenamed");
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
            "SubDir moved to FolderB");
        inspector.GetFileContent("Project/FolderB/SubDir/nested.txt")
            .Should().Contain("Edited after move");
        inspector.FileExists("Project/FolderB/stay.txt").Should().BeTrue();
    }

    [Fact]
    public void Migration_MovedDirectoryShouldNotRemainAtOldLocation()
    {
        var inspector = _runner.Inspector!;

        inspector.DirectoryExists("Project/FolderRenamed/SubDir").Should().BeFalse(
            "SubDir moved to FolderB");
        inspector.FileExists("Project/FolderRenamed/SubDir/nested.txt").Should().BeFalse(
            "moved with SubDir");
    }

    [Fact]
    public void Migration_ExactFileList()
    {
        _runner.Inspector!.GetFileList()
            .Should().HaveCount(4, "newname.txt + casename.txt + stay.txt + nested.txt");
    }

    [Fact]
    public void Migration_CommitQuality()
    {
        var commits = _runner.Inspector!.GetCommits();

        commits.Should().HaveCount(10);

        var withMessage = commits.Count(c => !string.IsNullOrWhiteSpace(c.Subject));
        withMessage.Should().Be(6);
    }

    public void Dispose() => _runner.Dispose();
}
