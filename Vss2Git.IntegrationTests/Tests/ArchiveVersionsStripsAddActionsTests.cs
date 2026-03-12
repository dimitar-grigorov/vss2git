using FluentAssertions;
using Hpdi.Vss2Git.IntegrationTests.Helpers;

namespace Hpdi.Vss2Git.IntegrationTests.Tests;

/// <summary>
/// Integration tests for Scenario15_ArchiveVersionsStripsAddActions.
/// Verifies that SeedProjectTree restores parent-child links when ssarc -v
/// strips Add actions from project history.
/// </summary>
public class ArchiveVersionsStripsAddActionsTests : IDisposable
{
    private readonly MigrationTestRunner _runner = new();

    public ArchiveVersionsStripsAddActionsTests()
    {
        _runner.Run("15_ArchiveVersionsStripsAddActions");
    }

    [Fact]
    public void Migration_Completes()
    {
        _runner.Inspector.Should().NotBeNull();
        _runner.Inspector!.GetCommitCount().Should().BeGreaterThan(0);
    }

    [Fact]
    public void Migration_SubProjectFilesExist()
    {
        var inspector = _runner.Inspector!;

        inspector.FileExists("ArcStrip/SubA/fileA.txt").Should().BeTrue(
            "SubA files should exist despite Add action being archived");
        inspector.FileExists("ArcStrip/SubB/fileB.txt").Should().BeTrue(
            "SubB files should exist despite Add action being archived");
    }

    [Fact]
    public void Migration_DeepNestedFileExists()
    {
        _runner.Inspector!.FileExists("ArcStrip/SubB/Deep/deep.txt").Should().BeTrue(
            "deeply nested file should exist — SeedProjectTree must seed full chain");
    }

    [Fact]
    public void Migration_PostArchiveEditsPresent()
    {
        var inspector = _runner.Inspector!;

        var fileA = inspector.GetFileContent("ArcStrip/SubA/fileA.txt");
        fileA.Should().Contain("after archive",
            "post-archive edit should be present");

        var fileB = inspector.GetFileContent("ArcStrip/SubB/fileB.txt");
        fileB.Should().Contain("after archive");

        var deep = inspector.GetFileContent("ArcStrip/SubB/Deep/deep.txt");
        deep.Should().Contain("after archive");
    }

    [Fact]
    public void Migration_NewFileAfterArchiveExists()
    {
        _runner.Inspector!.FileExists("ArcStrip/SubA/newfile.txt").Should().BeTrue(
            "file added after archive should exist");
    }

    [Fact]
    public void Migration_NewFileContent()
    {
        var content = _runner.Inspector!.GetFileContent("ArcStrip/SubA/newfile.txt");
        content.Should().Contain("New file added after archive");
    }

    [Fact]
    public void Migration_HasAfterArchiveTag()
    {
        _runner.Inspector!.GetTags().Should().Contain("after-archive");
    }

    [Fact]
    public void Migration_CleanWorkingDirectory()
    {
        _runner.Inspector!.HasCleanWorkingDirectory().Should().BeTrue(
            "no stale files after migration with archived Add actions");
    }

    [Fact]
    public void Migration_AllSubProjectDirectoriesExist()
    {
        var inspector = _runner.Inspector!;

        inspector.DirectoryExists("ArcStrip/SubA").Should().BeTrue();
        inspector.DirectoryExists("ArcStrip/SubB").Should().BeTrue();
        inspector.DirectoryExists("ArcStrip/SubB/Deep").Should().BeTrue();
    }

    public void Dispose() => _runner.Dispose();
}
