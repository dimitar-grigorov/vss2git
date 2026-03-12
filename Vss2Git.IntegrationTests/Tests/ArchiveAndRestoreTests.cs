using System.Linq;
using FluentAssertions;
using Hpdi.Vss2Git.IntegrationTests.Helpers;

namespace Hpdi.Vss2Git.IntegrationTests.Tests;

/// <summary>
/// Integration tests for Scenario14_ArchiveAndRestore.
/// </summary>
public class ArchiveAndRestoreTests : IDisposable
{
    private readonly MigrationTestRunner _runner = new();

    public ArchiveAndRestoreTests()
    {
        _runner.Run("14_ArchiveAndRestore");
    }

    [Fact]
    public void Migration_Completes()
    {
        _runner.Inspector.Should().NotBeNull();
        _runner.Inspector!.GetCommitCount().Should().BeGreaterThan(0);
    }

    [Fact]
    public void Migration_ArchivedFileRemovedThenRestored()
    {
        // archive-me.txt was archived (removed) then restored — should exist at HEAD
        _runner.Inspector!.FileExists("ArcTest/FileArchive/archive-me.txt").Should().BeTrue(
            "file was restored after archive");
    }

    [Fact]
    public void Migration_RestoredFileHasPostRestoreContent()
    {
        var content = _runner.Inspector!.GetFileContent("ArcTest/FileArchive/archive-me.txt");
        content.Should().Contain("Restored and edited",
            "file was edited after restore");
    }

    [Fact]
    public void Migration_StayFileExists()
    {
        _runner.Inspector!.FileExists("ArcTest/FileArchive/stay.txt").Should().BeTrue(
            "stay.txt was never archived");
    }

    [Fact]
    public void Migration_StayFileHasEditedContent()
    {
        var content = _runner.Inspector!.GetFileContent("ArcTest/FileArchive/stay.txt");
        content.Should().Contain("Edited after archive");
    }

    [Fact]
    public void Migration_VersionArchivedFileExists()
    {
        // versioned.txt had old versions archived (-v2) but file stays
        _runner.Inspector!.FileExists("ArcTest/VersionArchive/versioned.txt").Should().BeTrue(
            "version-only archive does not remove the file");
    }

    [Fact]
    public void Migration_VersionArchivedFileHasLatestContent()
    {
        var content = _runner.Inspector!.GetFileContent("ArcTest/VersionArchive/versioned.txt");
        content.Should().Contain("Version 5",
            "latest edit after version archive should be present");
    }

    [Fact]
    public void Migration_ArchivedProjectRestoredWithFiles()
    {
        var inspector = _runner.Inspector!;

        // ProjectArchive was archived (removed) then restored
        inspector.DirectoryExists("ArcTest/ProjectArchive").Should().BeTrue(
            "project was restored after archive");
        inspector.FileExists("ArcTest/ProjectArchive/proj-file1.txt").Should().BeTrue();
        inspector.FileExists("ArcTest/ProjectArchive/proj-file2.txt").Should().BeTrue();
    }

    [Fact]
    public void Migration_RestoredProjectFileEdited()
    {
        var content = _runner.Inspector!.GetFileContent("ArcTest/ProjectArchive/proj-file1.txt");
        content.Should().Contain("restored and edited");
    }

    [Fact]
    public void Migration_UnaffectedFileExists()
    {
        var content = _runner.Inspector!.GetFileContent("ArcTest/Unaffected/safe.txt");
        content.Should().Contain("Still safe after all operations");
    }

    [Fact]
    public void Migration_HasExpectedTags()
    {
        var tags = _runner.Inspector!.GetTags();
        tags.Should().Contain("before-archive");
        tags.Should().Contain("after-archive");
        tags.Should().Contain("after-restore");
    }

    [Fact]
    public void Migration_CleanWorkingDirectory()
    {
        _runner.Inspector!.HasCleanWorkingDirectory().Should().BeTrue(
            "no stale files should remain after archive/restore migration");
    }

    [Fact]
    public void Migration_ArchiveCommitRemovesFile()
    {
        // The commit history should show the file being removed then re-added
        var commits = _runner.Inspector!.GetCommits();
        commits.Should().Contain(c => c.Subject.Contains("Archive") || c.Subject.Contains("archive"),
            "there should be a commit related to the archive operation");
    }

    public void Dispose() => _runner.Dispose();
}
