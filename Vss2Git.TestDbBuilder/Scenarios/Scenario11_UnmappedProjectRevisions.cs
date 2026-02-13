namespace Hpdi.Vss2Git.TestDbBuilder.Scenarios;

/// <summary>
/// Sub-project migration: $/Staging/Worker has history, then is moved into $/Target.
/// Migration runs from $/Target only. Pre-move revisions are "unmapped" (no-ops).
/// Verifies MoveFrom's writeProject correctly populates files despite unmapped history.
/// </summary>
public class Scenario11_UnmappedProjectRevisions : ITestScenario
{
    public string Name => "11_UnmappedProjectRevisions";
    public string Description => "Sub-project migration with pre-move history (unmapped revisions)";

    public void Build(VssCommandRunner runner)
    {
        runner.CreateProject("$/Staging", "Outside migration scope");
        runner.CreateProject("$/Target", "Migration target root");
        runner.CreateProject("$/Staging/Worker", "Project with pre-move history");

        // Build history while outside scope
        runner.CreateAndAddFile("$/Staging/Worker", "config.txt",
            "config v1 - initial\n", "Add config");

        runner.EditFile("$/Staging/Worker", "config.txt",
            "config v2 - updated settings\n", "Update config v2");

        runner.CreateAndAddFile("$/Staging/Worker", "code.txt",
            "code v1 - initial implementation\n", "Add code file");

        runner.EditFile("$/Staging/Worker", "code.txt",
            "code v2 - refactored\n", "Refactor code v2");

        runner.EditFile("$/Staging/Worker", "config.txt",
            "config v3 - final staging config\n", "Config v3");

        runner.CreateAndAddFile("$/Target", "readme.txt",
            "target project readme\n", "Add readme to target");

        runner.Move("$/Staging/Worker", "$/Target");

        // Post-move edits (now inside migration scope)
        runner.EditFile("$/Target/Worker", "config.txt",
            "config v4 - post-move update\n", "Config v4 after move");

        runner.EditFile("$/Target/Worker", "code.txt",
            "code v3 - post-move fix\n", "Code v3 after move");

        runner.CreateAndAddFile("$/Target/Worker", "new-after-move.txt",
            "file created after move\n", "Add file after move");
    }

    public void Verify(VssTestDatabaseVerifier verifier)
    {
        var db = verifier.OpenDatabase();

        verifier.VerifyProjectExists(db, "$/Target");
        verifier.VerifyProjectExists(db, "$/Target/Worker");

        verifier.VerifyFileExists(db, "$/Target/Worker", "config.txt");
        verifier.VerifyFileExists(db, "$/Target/Worker", "code.txt");
        verifier.VerifyFileExists(db, "$/Target/Worker", "new-after-move.txt");
        verifier.VerifyFileContent(db, "$/Target/Worker", "config.txt", 4,
            "config v4 - post-move update\n");
        verifier.VerifyFileContent(db, "$/Target/Worker", "code.txt", 3,
            "code v3 - post-move fix\n");

        verifier.VerifyFileExists(db, "$/Target", "readme.txt");

        verifier.PrintDatabaseSummary(db);
    }
}
