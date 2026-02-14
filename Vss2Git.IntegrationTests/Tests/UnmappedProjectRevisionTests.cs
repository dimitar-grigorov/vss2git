using System.Linq;
using FluentAssertions;
using Hpdi.Vss2Git.IntegrationTests.Helpers;

namespace Hpdi.Vss2Git.IntegrationTests.Tests;

/// <summary>
/// Sub-project migration with pre-move history (Scenario 11).
/// Migrates from $/Target only. Files are under Target/ prefix (default mapping).
/// </summary>
public class UnmappedProjectRevisionTests : IDisposable
{
    private readonly MigrationTestRunner _runner = new();

    public UnmappedProjectRevisionTests()
    {
        // Migrate from $/Target only — $/Staging is outside scope
        _runner.Run("11_UnmappedProjectRevisions", vssProject: "$/Target");
    }

    [Fact]
    public void Migration_ConfigFileHasPostMoveContent()
    {
        var inspector = _runner.Inspector!;

        inspector.FileExists("Target/Worker/config.txt").Should().BeTrue();
        inspector.GetFileContent("Target/Worker/config.txt")
            .Should().Contain("config v4 - post-move update");
    }

    [Fact]
    public void Migration_CodeFileHasPostMoveContent()
    {
        var inspector = _runner.Inspector!;

        inspector.FileExists("Target/Worker/code.txt").Should().BeTrue();
        inspector.GetFileContent("Target/Worker/code.txt")
            .Should().Contain("code v3 - post-move fix");
    }

    [Fact]
    public void Migration_NewFileAfterMoveExists()
    {
        var inspector = _runner.Inspector!;

        inspector.FileExists("Target/Worker/new-after-move.txt").Should().BeTrue();
        inspector.GetFileContent("Target/Worker/new-after-move.txt")
            .Should().Contain("file created after move");
    }

    [Fact]
    public void Migration_ReadmeInTargetRoot()
    {
        _runner.Inspector!.FileExists("Target/readme.txt").Should().BeTrue();
    }

    [Fact]
    public void Migration_ExactFileList()
    {
        var files = _runner.Inspector!.GetFileList();

        // Target/readme.txt + Target/Worker/{config.txt, code.txt, new-after-move.txt} = 4
        files.Should().HaveCount(4);
    }

    [Fact]
    public void Migration_NoStaleUnmappedFiles()
    {
        var inspector = _runner.Inspector!;

        // No files should exist under Staging (it's outside migration scope)
        inspector.DirectoryExists("Staging").Should().BeFalse(
            "Staging is outside the migration scope");
    }

    [Fact]
    public void Migration_CommitQuality()
    {
        var commits = _runner.Inspector!.GetCommits();

        commits.Should().HaveCount(5);

        var withMessage = commits.Count(c => !string.IsNullOrWhiteSpace(c.Subject));
        withMessage.Should().Be(4);
    }

    public void Dispose() => _runner.Dispose();
}
