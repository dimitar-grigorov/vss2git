namespace Hpdi.Vss2Git.TestDbBuilder.Scenarios;

/// <summary>
/// Tests delete, recover, and destroy operations.
/// Targets bug H2 (directory deletion loses files).
/// </summary>
public class Scenario05_DeleteAndRecover : ITestScenario
{
    public string Name => "05_DeleteAndRecover";
    public string Description => "Delete/recover file, destroy project, destroy file";

    public void Build(VssCommandRunner runner)
    {
        // Create project structure with three sub-projects:
        //   ToDelete  — will be soft-deleted (stays in VSS tree)
        //   ToDestroy — will be permanently destroyed (gone from VSS tree)
        //   KeepMe    — remains untouched
        runner.CreateProject("$/DelTest", "Delete test project");
        runner.CreateProject("$/DelTest/ToDelete", "Project to delete");
        runner.CreateProject("$/DelTest/ToDestroy", "Project to destroy");
        runner.CreateProject("$/DelTest/KeepMe", "Project to keep");

        // Add files to each project
        runner.CreateAndAddFile("$/DelTest", "root.txt",
            "Root file.\n",
            "Add root file");

        runner.CreateAndAddFile("$/DelTest/ToDelete", "deletable.txt",
            "This file will be deleted then recovered.\n",
            "Add deletable file");

        runner.CreateAndAddFile("$/DelTest/ToDelete", "also-delete.txt",
            "This file will be deleted with the project.\n",
            "Add another file in ToDelete");

        runner.CreateAndAddFile("$/DelTest/ToDestroy", "destroyed.txt",
            "This file will be permanently destroyed.\n",
            "Add file to destroy");

        runner.CreateAndAddFile("$/DelTest/KeepMe", "kept.txt",
            "This file stays.\n",
            "Add kept file");

        // Edit before deletion to verify history survives delete+recover
        runner.EditFile("$/DelTest/ToDelete", "deletable.txt",
            "This file will be deleted then recovered.\nEdited before delete.\n",
            "Edit before delete");

        // Delete and recover a single file
        runner.Delete("$/DelTest/ToDelete/deletable.txt");
        runner.Recover("$/DelTest/ToDelete/deletable.txt");

        // Edit after recovery to verify file is fully functional
        runner.EditFile("$/DelTest/ToDelete", "deletable.txt",
            "This file will be deleted then recovered.\nEdited before delete.\nEdited after recovery.\n",
            "Edit after recovery");

        // Delete the entire ToDelete project (soft delete — stays in VSS tree)
        runner.Delete("$/DelTest/ToDelete");

        // Destroy file and project permanently (removed from VSS tree entirely)
        runner.Destroy("$/DelTest/ToDestroy/destroyed.txt");
        runner.Destroy("$/DelTest/ToDestroy");

        // Edit kept file to verify it's unaffected by other deletions
        runner.EditFile("$/DelTest/KeepMe", "kept.txt",
            "This file stays.\nStill here after other deletions.\n",
            "Edit after other deletions");

        runner.Label("$/DelTest", "after-deletions", "State after all deletions");
    }

    public void Verify(VssTestDatabaseVerifier verifier)
    {
        var db = verifier.OpenDatabase();

        verifier.VerifyProjectExists(db, "$/DelTest");
        verifier.VerifyProjectExists(db, "$/DelTest/KeepMe");

        // Root file should still exist
        verifier.VerifyFileExists(db, "$/DelTest", "root.txt");

        // kept.txt should have 2 versions (add + edit after other deletions)
        verifier.VerifyFileRevisionCount(db, "$/DelTest/KeepMe", "kept.txt", 2);
        verifier.VerifyFileContent(db, "$/DelTest/KeepMe", "kept.txt", 2,
            "This file stays.\nStill here after other deletions.\n");

        // ToDelete is soft-deleted (still in VSS tree), ToDestroy is destroyed (gone)
        // So DelTest has 2 sub-projects: KeepMe + ToDelete(deleted)
        verifier.VerifyProjectCount(db, "$/DelTest", 2);

        verifier.PrintDatabaseSummary(db);
    }
}
