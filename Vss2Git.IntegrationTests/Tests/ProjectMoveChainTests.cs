using System.Linq;
using FluentAssertions;
using Hpdi.Vss2Git.IntegrationTests.Helpers;

namespace Hpdi.Vss2Git.IntegrationTests.Tests;

/// <summary>
/// Integration tests for Scenario08_ProjectMoveChain.
/// Tests sequential project moves + rename, verifies old locations are cleaned up.
/// </summary>
public class ProjectMoveChainTests : IDisposable
{
    private readonly MigrationTestRunner _runner = new();

    public ProjectMoveChainTests()
    {
        _runner.Run("08_ProjectMoveChain");
    }

    [Fact]
    public void Migration_FinalFileLocation()
    {
        var inspector = _runner.Inspector!;

        inspector.FileExists("MoveTest/DestB/FinalProject/file1.txt").Should().BeTrue();
        inspector.GetFileContent("MoveTest/DestB/FinalProject/file1.txt")
            .Should().Contain("final content");
    }

    [Fact]
    public void Migration_SourceSubProjectGone()
    {
        var inspector = _runner.Inspector!;

        inspector.DirectoryExists("MoveTest/Source/SubProject").Should().BeFalse(
            "SubProject was moved away from Source");
        inspector.FileExists("MoveTest/Source/SubProject/file1.txt").Should().BeFalse(
            "file1 moved with SubProject");
    }

    [Fact]
    public void Migration_DestASubProjectGone()
    {
        var inspector = _runner.Inspector!;

        inspector.DirectoryExists("MoveTest/DestA/SubProject").Should().BeFalse(
            "SubProject was moved from DestA to DestB");
    }

    [Fact]
    public void Migration_OldNameGone()
    {
        _runner.Inspector!.DirectoryExists("MoveTest/DestB/SubProject").Should().BeFalse(
            "SubProject was renamed to FinalProject");
    }

    [Fact]
    public void Migration_UntouchedFilePreserved()
    {
        var inspector = _runner.Inspector!;

        inspector.FileExists("MoveTest/Source/file2.txt").Should().BeTrue();
        inspector.GetFileContent("MoveTest/Source/file2.txt")
            .Should().Contain("file in source");
    }

    [Fact]
    public void Migration_ExactFileList()
    {
        var files = _runner.Inspector!.GetFileList();

        // file1.txt (in FinalProject) + file2.txt (in Source)
        files.Should().HaveCount(2);
    }

    [Fact]
    public void Migration_CommitQuality()
    {
        var commits = _runner.Inspector!.GetCommits();

        // Operations: add file1, add file2, move1, edit, move2, edit, rename, edit = ~8 commits
        commits.Should().HaveCountGreaterThanOrEqualTo(6);

        var withMessage = commits.Count(c => !string.IsNullOrWhiteSpace(c.Subject));
        withMessage.Should().BeGreaterThanOrEqualTo(3);
    }

    public void Dispose() => _runner.Dispose();
}
