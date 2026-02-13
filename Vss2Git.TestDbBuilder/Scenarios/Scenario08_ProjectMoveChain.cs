namespace Hpdi.Vss2Git.TestDbBuilder.Scenarios;

/// <summary>
/// Tests sequential project moves and rename.
/// Targets bug H1 (MoveFrom source path uses already-updated path)
/// and unmapped project revisions after moves.
/// </summary>
public class Scenario08_ProjectMoveChain : ITestScenario
{
    public string Name => "08_ProjectMoveChain";
    public string Description => "Sequential project moves + rename, verify old locations cleaned up";

    public void Build(VssCommandRunner runner)
    {
        // Create project structure
        runner.CreateProject("$/MoveTest", "Move test root");
        runner.CreateProject("$/MoveTest/Source", "Source container");
        runner.CreateProject("$/MoveTest/Source/SubProject", "Project to move");
        runner.CreateProject("$/MoveTest/DestA", "First destination");
        runner.CreateProject("$/MoveTest/DestB", "Second destination");

        // Add files before any moves
        runner.CreateAndAddFile("$/MoveTest/Source/SubProject", "file1.txt",
            "file in subproject\n",
            "Add file1 to SubProject");

        runner.CreateAndAddFile("$/MoveTest/Source", "file2.txt",
            "file in source\n",
            "Add file2 to Source");

        // Move SubProject: Source → DestA
        runner.Move("$/MoveTest/Source/SubProject", "$/MoveTest/DestA");

        // Edit file1 after first move (now in DestA/SubProject)
        runner.EditFile("$/MoveTest/DestA/SubProject", "file1.txt",
            "edited after first move\n",
            "Edit after move to DestA");

        // Move SubProject: DestA → DestB
        runner.Move("$/MoveTest/DestA/SubProject", "$/MoveTest/DestB");

        // Edit file1 after second move (now in DestB/SubProject)
        runner.EditFile("$/MoveTest/DestB/SubProject", "file1.txt",
            "edited after second move\n",
            "Edit after move to DestB");

        // Rename SubProject → FinalProject (in DestB)
        runner.Rename("$/MoveTest/DestB/SubProject", "FinalProject");

        // Edit file1 after rename (now in DestB/FinalProject)
        runner.EditFile("$/MoveTest/DestB/FinalProject", "file1.txt",
            "final content\n",
            "Final edit after rename");
    }

    public void Verify(VssTestDatabaseVerifier verifier)
    {
        var db = verifier.OpenDatabase();

        // Verify project structure after moves
        verifier.VerifyProjectExists(db, "$/MoveTest/Source");
        verifier.VerifyProjectExists(db, "$/MoveTest/DestA");
        verifier.VerifyProjectExists(db, "$/MoveTest/DestB");
        verifier.VerifyProjectExists(db, "$/MoveTest/DestB/FinalProject");

        // file1.txt should be in FinalProject with latest content
        verifier.VerifyFileExists(db, "$/MoveTest/DestB/FinalProject", "file1.txt");
        verifier.VerifyFileContent(db, "$/MoveTest/DestB/FinalProject", "file1.txt", 4,
            "final content\n");

        // file2.txt should still be in Source
        verifier.VerifyFileExists(db, "$/MoveTest/Source", "file2.txt");

        // Source should have no sub-projects (SubProject was moved out)
        verifier.VerifyProjectCount(db, "$/MoveTest/Source", 0);

        // DestA should have no sub-projects (SubProject was moved out)
        verifier.VerifyProjectCount(db, "$/MoveTest/DestA", 0);

        verifier.PrintDatabaseSummary(db);
    }
}
