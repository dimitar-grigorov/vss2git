namespace Hpdi.Vss2Git.TestDbBuilder.Scenarios;

/// <summary>
/// Scenario: ssarc -v on a parent project strips Add actions for sub-projects,
/// requiring SeedProjectTree to restore parent-child links during migration.
/// </summary>
public class Scenario15_ArchiveVersionsStripsAddActions : ITestScenario
{
    public string Name => "15_ArchiveVersionsStripsAddActions";
    public string Description => "Archive old versions of parent project, stripping Add actions for sub-projects";

    public void Build(VssCommandRunner runner)
    {
        // Create project structure
        runner.CreateProject("$/ArcStrip", "Root project");
        runner.CreateProject("$/ArcStrip/SubA", "Sub-project A");
        runner.CreateProject("$/ArcStrip/SubB", "Sub-project B");
        runner.CreateProject("$/ArcStrip/SubB/Deep", "Deeply nested project");

        // Add files
        runner.CreateAndAddFile("$/ArcStrip/SubA", "fileA.txt",
            "File A version 1.\n", "Add fileA");
        runner.CreateAndAddFile("$/ArcStrip/SubB", "fileB.txt",
            "File B version 1.\n", "Add fileB");
        runner.CreateAndAddFile("$/ArcStrip/SubB/Deep", "deep.txt",
            "Deep file version 1.\n", "Add deep file");

        // Build up version history
        runner.EditFile("$/ArcStrip/SubA", "fileA.txt",
            "File A version 2.\n", "Edit fileA v2");
        runner.EditFile("$/ArcStrip/SubB", "fileB.txt",
            "File B version 2.\n", "Edit fileB v2");

        // Archive old versions — strips Add actions from $/ArcStrip
        runner.Archive("$/ArcStrip", "root-versions.ssa",
            upToVersion: 3, comment: "Archive early versions of root");

        // Edits after archive — should appear in git
        runner.EditFile("$/ArcStrip/SubA", "fileA.txt",
            "File A version 3 (after archive).\n", "Edit fileA after archive");
        runner.EditFile("$/ArcStrip/SubB", "fileB.txt",
            "File B version 3 (after archive).\n", "Edit fileB after archive");
        runner.EditFile("$/ArcStrip/SubB/Deep", "deep.txt",
            "Deep file version 2 (after archive).\n", "Edit deep after archive");

        runner.CreateAndAddFile("$/ArcStrip/SubA", "newfile.txt",
            "New file added after archive.\n", "Add new file after archive");

        runner.Label("$/ArcStrip", "after-archive", "State after archiving old versions");
    }

    public void Verify(VssTestDatabaseVerifier verifier)
    {
        var db = verifier.OpenDatabase();

        verifier.VerifyProjectExists(db, "$/ArcStrip");
        verifier.VerifyProjectExists(db, "$/ArcStrip/SubA");
        verifier.VerifyProjectExists(db, "$/ArcStrip/SubB");
        verifier.VerifyProjectExists(db, "$/ArcStrip/SubB/Deep");

        verifier.VerifyFileExists(db, "$/ArcStrip/SubA", "fileA.txt");
        verifier.VerifyFileExists(db, "$/ArcStrip/SubA", "newfile.txt");
        verifier.VerifyFileExists(db, "$/ArcStrip/SubB", "fileB.txt");
        verifier.VerifyFileExists(db, "$/ArcStrip/SubB/Deep", "deep.txt");

        verifier.PrintDatabaseSummary(db);
    }
}
