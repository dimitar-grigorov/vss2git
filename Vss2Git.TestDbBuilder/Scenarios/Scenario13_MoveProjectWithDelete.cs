namespace Hpdi.Vss2Git.TestDbBuilder.Scenarios;

/// <summary>
/// Reproduces stale working directory files when a subproject is moved into
/// a project that already contains files, and the existing files are deleted
/// in the same changeset.
///
/// Real-world pattern: OfficeMgr/Source moved into SLDepotSW while
/// SLDepotSW's root-level files were deleted simultaneously.
/// The root-level files remained on disk after migration.
/// </summary>
public class Scenario13_MoveProjectWithDelete : ITestScenario
{
    public string Name => "13_MoveProjectWithDelete";
    public string Description => "Move subproject into project with simultaneous file deletes";

    public void Build(VssCommandRunner runner)
    {
        // Create project structure
        runner.CreateProject("$/App", "Application root");
        runner.CreateProject("$/App/Dest", "Destination project (has root-level files)");
        runner.CreateProject("$/App/Src", "Source container");
        runner.CreateProject("$/App/Src/Code", "Subproject to be moved");

        // Add files to Dest at root level (these will be deleted later)
        runner.CreateAndAddFile("$/App/Dest", "main.pas",
            "unit main; // root level\n",
            "Add main.pas to Dest root");

        runner.CreateAndAddFile("$/App/Dest", "config.dfm",
            "object Form1: TForm1\nend\n",
            "Add config.dfm to Dest root");

        runner.CreateAndAddFile("$/App/Dest", "utils.pas",
            "unit utils; // root level\n",
            "Add utils.pas to Dest root");

        // Add files to Src/Code (these will be moved into Dest)
        runner.CreateAndAddFile("$/App/Src/Code", "main.pas",
            "unit main; // in Code subfolder\n",
            "Add main.pas to Code");

        runner.CreateAndAddFile("$/App/Src/Code", "helper.pas",
            "unit helper;\n",
            "Add helper.pas to Code");

        // Edit a file to create some history
        runner.EditFile("$/App/Dest", "main.pas",
            "unit main; // root level v2\n",
            "Edit root main.pas");

        // --- Critical: Move + Delete at same timestamp ---
        // This reproduces the real-world scenario where VSS operations
        // happen at the same second, causing them to be in one changeset.
        var savedDelay = runner.DelayAfterRevision;
        runner.DelayAfterRevision = TimeSpan.Zero;

        // Move Code subproject from Src into Dest
        runner.Move("$/App/Src/Code", "$/App/Dest");

        // Delete root-level files from Dest (superseded by Code/ files)
        runner.Delete("$/App/Dest/main.pas");
        runner.Delete("$/App/Dest/config.dfm");
        runner.Delete("$/App/Dest/utils.pas");

        runner.DelayAfterRevision = savedDelay;

        // Edit a file in the moved subproject to confirm it works
        runner.EditFile("$/App/Dest/Code", "main.pas",
            "unit main; // after move into Dest\n",
            "Edit main.pas after move");
    }

    public void Verify(VssTestDatabaseVerifier verifier)
    {
        var db = verifier.OpenDatabase();

        // Code subproject should now be under Dest
        verifier.VerifyProjectExists(db, "$/App/Dest/Code");

        // Files in Code should exist
        verifier.VerifyFileExists(db, "$/App/Dest/Code", "main.pas");
        verifier.VerifyFileExists(db, "$/App/Dest/Code", "helper.pas");

        // Root-level files in Dest are deleted (VSS still lists them — delete is a flag, not removal)
        verifier.VerifyFileCount(db, "$/App/Dest", 3);

        // Src should have no subprojects (Code was moved out)
        verifier.VerifyProjectCount(db, "$/App/Src", 0);

        verifier.PrintDatabaseSummary(db);
    }
}
