using System.Linq;
using FluentAssertions;
using Hpdi.Vss2Git.IntegrationTests.Helpers;

namespace Hpdi.Vss2Git.IntegrationTests.Tests;

/// <summary>
/// Integration tests for Scenario05_DeleteAndRecover.
/// </summary>
public class DeleteAndRecoverTests : IDisposable
{
    private readonly MigrationTestRunner _runner = new();

    public DeleteAndRecoverTests()
    {
        _runner.Run("05_DeleteAndRecover");
    }

    [Fact]
    public void Migration_SurvivingFilesIntact()
    {
        var inspector = _runner.Inspector!;

        inspector.FileExists("DelTest/root.txt").Should().BeTrue();
        inspector.DirectoryExists("DelTest/KeepMe").Should().BeTrue();
        inspector.GetFileContent("DelTest/KeepMe/kept.txt")
            .Should().Contain("Still here after other deletions");
    }

    [Fact]
    public void Migration_DestroyedItemsRemoved()
    {
        var inspector = _runner.Inspector!;

        inspector.DirectoryExists("DelTest/ToDestroy").Should().BeFalse("destroyed");
        inspector.FileExists("DelTest/ToDestroy/destroyed.txt").Should().BeFalse("destroyed");
    }

    [Fact]
    public void Migration_DeletedProjectFullyRemoved()
    {
        var inspector = _runner.Inspector!;

        inspector.DirectoryExists("DelTest/ToDelete").Should().BeFalse("project was deleted");
        inspector.FileExists("DelTest/ToDelete/also-delete.txt").Should().BeFalse("project was deleted");
        inspector.FileExists("DelTest/ToDelete/deletable.txt").Should().BeFalse(
            "recovered then project deleted");
    }

    [Fact]
    public void Migration_TagExists()
    {
        _runner.Inspector!.GetTags().Should().Contain(t =>
            t.Contains("after") && t.Contains("deletion"));
    }

    [Fact]
    public void Migration_ExactFileList()
    {
        var files = _runner.Inspector!.GetFileList();

        files.Should().HaveCount(2, "root.txt + kept.txt");
    }

    [Fact]
    public void Migration_CommitQuality()
    {
        var commits = _runner.Inspector!.GetCommits();

        commits.Should().HaveCountGreaterThanOrEqualTo(5);

        var withMessage = commits.Count(c => !string.IsNullOrWhiteSpace(c.Subject));
        withMessage.Should().BeGreaterThanOrEqualTo(4);
    }

    public void Dispose() => _runner.Dispose();
}
