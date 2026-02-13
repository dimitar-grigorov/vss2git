namespace Hpdi.Vss2Git.TestDbBuilder.Scenarios;

/// <summary>
/// Tests file/project renames, case-only renames, and moves.
/// Targets bug H1 (file moves produce inconsistent results).
/// </summary>
public class Scenario02_RenamesAndMoves : ITestScenario
{
    public string Name => "02_RenamesAndMoves";
    public string Description => "Rename files, rename projects, case-only rename, move project";

    public void Build(VssCommandRunner runner)
    {
        // Create project structure: FolderA with SubDir, FolderB
        runner.CreateProject("$/Project", "Root project");
        runner.CreateProject("$/Project/FolderA", "Folder A");
        runner.CreateProject("$/Project/FolderB", "Folder B");
        runner.CreateProject("$/Project/FolderA/SubDir", "Sub directory in A");

        // Add files to various locations
        runner.CreateAndAddFile("$/Project/FolderA", "oldname.txt",
            "File with original name.\n",
            "Add file with original name");

        runner.CreateAndAddFile("$/Project/FolderA", "CaseName.txt",
            "File for case-only rename test.\n",
            "Add file for case rename");

        runner.CreateAndAddFile("$/Project/FolderA/SubDir", "nested.txt",
            "Nested file in SubDir.\n",
            "Add nested file");

        runner.CreateAndAddFile("$/Project/FolderB", "stay.txt",
            "This file stays in FolderB.\n",
            "Add file that stays");

        // Rename file: oldname.txt -> newname.txt
        runner.Rename("$/Project/FolderA/oldname.txt", "newname.txt");

        // Edit renamed file to verify it's still accessible
        runner.EditFile("$/Project/FolderA", "newname.txt",
            "File with new name.\nEdited after rename.\n",
            "Edit after rename");

        // Case-only rename: CaseName.txt -> casename.txt
        runner.Rename("$/Project/FolderA/CaseName.txt", "casename.txt");

        // Rename project: FolderA -> FolderRenamed
        runner.Rename("$/Project/FolderA", "FolderRenamed");

        // Move SubDir from FolderRenamed to FolderB
        runner.Move("$/Project/FolderRenamed/SubDir", "$/Project/FolderB");

        // Edit file in moved directory to verify it's still accessible
        runner.EditFile("$/Project/FolderB/SubDir", "nested.txt",
            "Nested file in SubDir.\nEdited after move to FolderB.\n",
            "Edit after move");

        runner.Label("$/Project", "after-moves", "State after all renames and moves");
    }

    public void Verify(VssTestDatabaseVerifier verifier)
    {
        var db = verifier.OpenDatabase();

        // Verify project structure after renames and moves
        verifier.VerifyProjectExists(db, "$/Project");
        verifier.VerifyProjectExists(db, "$/Project/FolderRenamed");  // was FolderA
        verifier.VerifyProjectExists(db, "$/Project/FolderB");
        verifier.VerifyProjectExists(db, "$/Project/FolderB/SubDir");  // moved from FolderRenamed

        // Verify renamed files are at their new names
        verifier.VerifyFileExists(db, "$/Project/FolderRenamed", "newname.txt");  // was oldname.txt
        verifier.VerifyFileExists(db, "$/Project/FolderRenamed", "casename.txt");  // was CaseName.txt
        verifier.VerifyFileExists(db, "$/Project/FolderB/SubDir", "nested.txt");  // moved with SubDir

        // Verify revision counts (add + edit = 2 for edited files)
        verifier.VerifyFileRevisionCount(db, "$/Project/FolderRenamed", "newname.txt", 2);
        verifier.VerifyFileRevisionCount(db, "$/Project/FolderB/SubDir", "nested.txt", 2);

        // Verify FolderB has stay.txt and SubDir (moved into it)
        verifier.VerifyFileExists(db, "$/Project/FolderB", "stay.txt");
        verifier.VerifyProjectCount(db, "$/Project/FolderB", 1);  // SubDir

        verifier.PrintDatabaseSummary(db);
    }
}
