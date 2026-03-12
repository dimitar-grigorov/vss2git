namespace Hpdi.Vss2Git.TestDbBuilder.Scenarios;

/// <summary>
/// Tests archive and restore operations using ssarc.exe / ssrestor.exe.
/// Targets bug archive action ignored and archive sub-types merged.
/// </summary>
public class Scenario14_ArchiveAndRestore : ITestScenario
{
    public string Name => "14_ArchiveAndRestore";
    public string Description => "Archive files/projects with ssarc.exe, restore with ssrestor.exe";

    public void Build(VssCommandRunner runner)
    {
        // Setup project structure
        runner.CreateProject("$/ArcTest", "Archive test root");
        runner.CreateProject("$/ArcTest/FileArchive", "File archive tests");
        runner.CreateProject("$/ArcTest/VersionArchive", "Version archive tests");
        runner.CreateProject("$/ArcTest/ProjectArchive", "Project to be archived");
        runner.CreateProject("$/ArcTest/ProjectArchive/SubProj", "Sub-project inside archived project");
        runner.CreateProject("$/ArcTest/Unaffected", "Unaffected project");

        runner.CreateAndAddFile("$/ArcTest/FileArchive", "archive-me.txt",
            "This file will be archived and deleted.\n", "Add file to archive");
        runner.EditFile("$/ArcTest/FileArchive", "archive-me.txt",
            "This file will be archived and deleted.\nEdited v2.\n", "Edit before archive");
        runner.EditFile("$/ArcTest/FileArchive", "archive-me.txt",
            "This file will be archived and deleted.\nEdited v2.\nEdited v3.\n", "Edit before archive v3");
        runner.CreateAndAddFile("$/ArcTest/FileArchive", "stay.txt",
            "This file stays in the project.\n", "Add file that stays");

        runner.CreateAndAddFile("$/ArcTest/VersionArchive", "versioned.txt",
            "Version 1 content.\n", "Add versioned file");
        runner.EditFile("$/ArcTest/VersionArchive", "versioned.txt",
            "Version 2 content.\n", "Edit to v2");
        runner.EditFile("$/ArcTest/VersionArchive", "versioned.txt",
            "Version 3 content.\n", "Edit to v3");
        runner.EditFile("$/ArcTest/VersionArchive", "versioned.txt",
            "Version 4 content.\n", "Edit to v4");

        runner.CreateAndAddFile("$/ArcTest/ProjectArchive", "proj-file1.txt",
            "Project file 1.\n", "Add project file 1");
        runner.CreateAndAddFile("$/ArcTest/ProjectArchive", "proj-file2.txt",
            "Project file 2.\n", "Add project file 2");
        runner.CreateAndAddFile("$/ArcTest/ProjectArchive/SubProj", "sub-file.txt",
            "Sub-project file.\n", "Add sub-project file");

        runner.CreateAndAddFile("$/ArcTest/Unaffected", "safe.txt",
            "This file is unaffected by archives.\n", "Add safe file");

        runner.Label("$/ArcTest", "before-archive", "State before any archive operations");

        // Case 1: Archive file with delete — removes file from project
        runner.Archive("$/ArcTest/FileArchive/archive-me.txt", "file-archive.ssa",
            deleteAfterArchive: true, comment: "Archive file with delete");
        runner.EditFile("$/ArcTest/FileArchive", "stay.txt",
            "This file stays in the project.\nEdited after archive.\n",
            "Edit stay.txt after file archive");

        // Case 2: Archive old versions (-v2), file stays, old history removed
        runner.Archive("$/ArcTest/VersionArchive/versioned.txt", "versions-archive.ssa",
            upToVersion: 2, comment: "Archive old versions up to v2");
        runner.EditFile("$/ArcTest/VersionArchive", "versioned.txt",
            "Version 5 content.\n", "Edit after version archive");

        // Case 3: Archive entire project with delete
        runner.Archive("$/ArcTest/ProjectArchive", "project-archive.ssa",
            deleteAfterArchive: true, comment: "Archive entire project");

        runner.Label("$/ArcTest", "after-archive", "State after all archive operations");

        // Case 4: Restore archived file
        runner.Restore("file-archive.ssa", "$/ArcTest/FileArchive/archive-me.txt");
        runner.EditFile("$/ArcTest/FileArchive", "archive-me.txt",
            "Restored and edited.\n", "Edit after restore");

        // Case 5: Restore archived project
        runner.Restore("project-archive.ssa", "$/ArcTest/ProjectArchive");
        runner.EditFile("$/ArcTest/ProjectArchive", "proj-file1.txt",
            "Project file 1 - restored and edited.\n", "Edit after project restore");

        runner.EditFile("$/ArcTest/Unaffected", "safe.txt",
            "This file is unaffected by archives.\nStill safe after all operations.\n",
            "Edit safe file after all archive/restore");

        runner.Label("$/ArcTest", "after-restore", "State after restore operations");
    }

    public void Verify(VssTestDatabaseVerifier verifier)
    {
        var db = verifier.OpenDatabase();

        verifier.VerifyProjectExists(db, "$/ArcTest");
        verifier.VerifyProjectExists(db, "$/ArcTest/FileArchive");
        verifier.VerifyProjectExists(db, "$/ArcTest/VersionArchive");
        verifier.VerifyProjectExists(db, "$/ArcTest/Unaffected");
        verifier.VerifyProjectExists(db, "$/ArcTest/ProjectArchive");

        verifier.VerifyFileExists(db, "$/ArcTest/FileArchive", "archive-me.txt");
        verifier.VerifyFileExists(db, "$/ArcTest/FileArchive", "stay.txt");
        verifier.VerifyFileExists(db, "$/ArcTest/VersionArchive", "versioned.txt");
        verifier.VerifyFileRevisionCount(db, "$/ArcTest/Unaffected", "safe.txt", 2);

        verifier.PrintDatabaseSummary(db);
    }
}
