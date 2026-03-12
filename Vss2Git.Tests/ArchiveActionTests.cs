using FluentAssertions;
using Hpdi.VssLogicalLib;

namespace Hpdi.Vss2Git.Tests;

/// <summary>
/// Tests for archive/restore action sub-type mapping and RemovesItem/AddsItem semantics.
/// </summary>
public class ArchiveActionTests
{
    private static VssItemName MakeItemName(string logical, string physical, bool isProject = false)
    {
        return new VssItemName(logical, physical, isProject);
    }

    #region VssArchiveAction SubType and RemovesItem

    [Theory]
    [InlineData(VssArchiveSubType.File, true)]
    [InlineData(VssArchiveSubType.Project, true)]
    [InlineData(VssArchiveSubType.Versions, false)]
    [InlineData(VssArchiveSubType.All, false)]
    public void ArchiveAction_RemovesItem_MatchesSubType(VssArchiveSubType subType, bool expectedRemoves)
    {
        var action = new VssArchiveAction(
            MakeItemName("test.txt", "AAAAAAAA"), "/archive.ssa", subType);

        action.RemovesItem.Should().Be(expectedRemoves);
    }

    [Fact]
    public void ArchiveAction_Type_IsArchive()
    {
        var action = new VssArchiveAction(
            MakeItemName("test.txt", "AAAAAAAA"), "/archive.ssa", VssArchiveSubType.File);

        action.Type.Should().Be(VssActionType.Archive);
    }

    [Fact]
    public void ArchiveAction_PreservesArchivePath()
    {
        var action = new VssArchiveAction(
            MakeItemName("test.txt", "AAAAAAAA"), @"C:\archives\test.ssa", VssArchiveSubType.File);

        action.ArchivePath.Should().Be(@"C:\archives\test.ssa");
    }

    [Fact]
    public void ArchiveAction_ToString_IncludesSubType()
    {
        var action = new VssArchiveAction(
            MakeItemName("test.txt", "AAAAAAAA"), "/archive.ssa", VssArchiveSubType.Project);

        action.ToString().Should().Contain("Project");
    }

    #endregion

    #region VssRestoreAction SubType and AddsItem

    [Theory]
    [InlineData(VssRestoreSubType.File)]
    [InlineData(VssRestoreSubType.Project)]
    public void RestoreAction_AddsItem_AlwaysTrue(VssRestoreSubType subType)
    {
        var action = new VssRestoreAction(
            MakeItemName("test.txt", "AAAAAAAA"), "/archive.ssa", subType);

        action.AddsItem.Should().BeTrue("all restore actions add a visible item");
    }

    [Fact]
    public void RestoreAction_Type_IsRestore()
    {
        var action = new VssRestoreAction(
            MakeItemName("test.txt", "AAAAAAAA"), "/archive.ssa", VssRestoreSubType.File);

        action.Type.Should().Be(VssActionType.Restore);
    }

    [Fact]
    public void RestoreAction_PreservesArchivePath()
    {
        var action = new VssRestoreAction(
            MakeItemName("test.txt", "AAAAAAAA"), @"C:\archives\test.ssa", VssRestoreSubType.File);

        action.ArchivePath.Should().Be(@"C:\archives\test.ssa");
    }

    [Fact]
    public void RestoreAction_ToString_IncludesSubType()
    {
        var action = new VssRestoreAction(
            MakeItemName("test.txt", "AAAAAAAA"), "/archive.ssa", VssRestoreSubType.Project);

        action.ToString().Should().Contain("Project");
    }

    #endregion

    #region Physical-to-logical mapping (observed VSS behavior)

    /// <summary>
    /// RestoreVersions(22) is recorded by ssarc -d on a FILE.
    /// Despite the misleading name ("RestoreVersions" = "export versions to archive"),
    /// the file IS removed from the project.
    /// Fix: remapped to VssArchiveAction(File) so GitExporter correctly deletes it.
    /// </summary>
    [Fact]
    public void RestoreVersions_RemappedToArchiveFile_RemovesItem()
    {
        // RestoreVersions(22) is now mapped to VssArchiveAction(File) in VssRevision.cs
        var action = new VssArchiveAction(
            MakeItemName("archive-me.txt", "HAAAAAAA"), "/file-archive.ssa",
            VssArchiveSubType.File);

        action.Type.Should().Be(VssActionType.Archive);
        action.RemovesItem.Should().BeTrue("ssarc -d removes the file from the project");
    }

    /// <summary>
    /// RestoreFile(24) is recorded by ssrestor on a FILE.
    /// The file IS added back to the project.
    /// </summary>
    [Fact]
    public void RestoreFile_AddsItem()
    {
        var action = new VssRestoreAction(
            MakeItemName("archive-me.txt", "HAAAAAAA"), "/file-archive.ssa",
            VssRestoreSubType.File);

        action.AddsItem.Should().BeTrue("RestoreFile adds the file back");
    }

    /// <summary>
    /// ArchiveProject(23) is recorded by ssarc -d on a PROJECT.
    /// The project IS removed.
    /// </summary>
    [Fact]
    public void ArchiveProject_RemovesItem()
    {
        var action = new VssArchiveAction(
            MakeItemName("ProjectArchive", "EAAAAAAA", isProject: true), "/project.ssa",
            VssArchiveSubType.Project);

        action.RemovesItem.Should().BeTrue();
    }

    /// <summary>
    /// RestoreProject(25) is recorded by ssrestor on a PROJECT.
    /// The project IS added back.
    /// </summary>
    [Fact]
    public void RestoreProject_AddsItem()
    {
        var action = new VssRestoreAction(
            MakeItemName("ProjectArchive", "EAAAAAAA", isProject: true), "/project.ssa",
            VssRestoreSubType.Project);

        action.AddsItem.Should().BeTrue();
    }

    /// <summary>
    /// ArchiveVersions(20) — archive old versions only, file stays.
    /// ssarc -v2 records NO action in VSS history; if it appears, should not remove.
    /// </summary>
    [Fact]
    public void ArchiveVersions_DoesNotRemoveItem()
    {
        var action = new VssArchiveAction(
            MakeItemName("versioned.txt", "JAAAAAAA"), "/versions.ssa",
            VssArchiveSubType.Versions);

        action.RemovesItem.Should().BeFalse();
    }

    #endregion

    #region Archive/Restore lifecycle tests

    /// <summary>
    /// File archive-then-restore lifecycle:
    ///   ssarc -d file → RestoreVersions(22) → now mapped to Archive(File) → removes file
    ///   ssrestor file → RestoreFile(24) → Restore(File) → adds file back
    /// </summary>
    [Fact]
    public void FileArchiveRestoreCycle_CorrectActionTypes()
    {
        var fileName = MakeItemName("archive-me.txt", "HAAAAAAA");

        // Step 1: ssarc -d → RestoreVersions(22) → remapped to Archive(File)
        var archiveAction = new VssArchiveAction(fileName, "/file.ssa", VssArchiveSubType.File);
        archiveAction.Type.Should().Be(VssActionType.Archive);
        archiveAction.RemovesItem.Should().BeTrue();

        // Step 2: ssrestor → RestoreFile(24) → Restore(File)
        var restoreAction = new VssRestoreAction(fileName, "/file.ssa", VssRestoreSubType.File);
        restoreAction.Type.Should().Be(VssActionType.Restore);
        restoreAction.AddsItem.Should().BeTrue();
    }

    /// <summary>
    /// Project archive-then-restore lifecycle:
    ///   ssarc -d $/Project → ArchiveProject(23) → Archive(Project) → removes project
    ///   ssrestor $/Project → RestoreProject(25) → Restore(Project) → adds project back
    /// </summary>
    [Fact]
    public void ProjectArchiveRestoreCycle_CorrectActionTypes()
    {
        var projectName = MakeItemName("ProjectArchive", "EAAAAAAA", isProject: true);

        var archiveAction = new VssArchiveAction(projectName, "/project.ssa", VssArchiveSubType.Project);
        archiveAction.Type.Should().Be(VssActionType.Archive);
        archiveAction.RemovesItem.Should().BeTrue();

        var restoreAction = new VssRestoreAction(projectName, "/project.ssa", VssRestoreSubType.Project);
        restoreAction.Type.Should().Be(VssActionType.Restore);
        restoreAction.AddsItem.Should().BeTrue();
    }

    #endregion

    #region Enum coverage

    [Fact]
    public void AllArchiveSubTypes_AreDistinct()
    {
        var subTypes = new[]
        {
            VssArchiveSubType.File,     // ArchiveFile(18) + RestoreVersions(22)
            VssArchiveSubType.Versions, // ArchiveVersions(20)
            VssArchiveSubType.All,      // ArchiveAll(21)
            VssArchiveSubType.Project   // ArchiveProject(23)
        };

        subTypes.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void AllRestoreSubTypes_AreDistinct()
    {
        var subTypes = new[]
        {
            VssRestoreSubType.File,    // RestoreFile(24)
            VssRestoreSubType.Project  // RestoreProject(25)
        };

        subTypes.Should().OnlyHaveUniqueItems();
    }

    #endregion
}
