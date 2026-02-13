namespace Hpdi.Vss2Git.TestDbBuilder.Scenarios;

/// <summary>
/// Tests project deletion + recovery with nested subprojects and files,
/// and file delete/recover within a project.
/// Targets bug H2 (directory deletion loses files) and nested project recovery.
/// </summary>
public class Scenario09_DeleteRecoverProject : ITestScenario
{
    public string Name => "09_DeleteRecoverProject";
    public string Description => "Delete/recover project with nested subprojects, then file delete/recover";

    public void Build(VssCommandRunner runner)
    {
        // Create nested project structure
        runner.CreateProject("$/DelRecoverTest", "Delete recover test root");
        runner.CreateProject("$/DelRecoverTest/Deletable", "Project to delete and recover");
        runner.CreateProject("$/DelRecoverTest/Deletable/Inner", "Nested sub-project");

        // Add files
        runner.CreateAndAddFile("$/DelRecoverTest/Deletable", "file1.txt",
            "deletable file\n",
            "Add file1");

        runner.CreateAndAddFile("$/DelRecoverTest/Deletable/Inner", "inner.txt",
            "inner file\n",
            "Add inner file");

        runner.CreateAndAddFile("$/DelRecoverTest", "root.txt",
            "root stays\n",
            "Add root file");

        // Edit files before project deletion
        runner.EditFile("$/DelRecoverTest/Deletable", "file1.txt",
            "edited before delete\n",
            "Edit file1 before delete");

        runner.EditFile("$/DelRecoverTest/Deletable/Inner", "inner.txt",
            "inner edited\n",
            "Edit inner before delete");

        // Delete entire Deletable project (soft delete)
        runner.Delete("$/DelRecoverTest/Deletable");

        // Edit root while Deletable is deleted
        runner.EditFile("$/DelRecoverTest", "root.txt",
            "root edited while Deletable is deleted\n",
            "Edit root while project deleted");

        // Recover the project
        runner.Recover("$/DelRecoverTest/Deletable");

        // Edit files after project recovery
        runner.EditFile("$/DelRecoverTest/Deletable", "file1.txt",
            "edited after project recovery\n",
            "Edit file1 after recovery");

        runner.EditFile("$/DelRecoverTest/Deletable/Inner", "inner.txt",
            "inner after recovery\n",
            "Edit inner after recovery");

        // File-level delete/recover cycle
        runner.Delete("$/DelRecoverTest/Deletable/file1.txt");

        runner.Recover("$/DelRecoverTest/Deletable/file1.txt");

        runner.EditFile("$/DelRecoverTest/Deletable", "file1.txt",
            "final after file recovery\n",
            "Final edit after file recovery");
    }

    public void Verify(VssTestDatabaseVerifier verifier)
    {
        var db = verifier.OpenDatabase();

        verifier.VerifyProjectExists(db, "$/DelRecoverTest");
        verifier.VerifyProjectExists(db, "$/DelRecoverTest/Deletable");
        verifier.VerifyProjectExists(db, "$/DelRecoverTest/Deletable/Inner");

        // file1.txt should have: add, edit, (project delete/recover don't add revisions),
        // edit after recovery, (file delete/recover), edit after file recovery
        verifier.VerifyFileExists(db, "$/DelRecoverTest/Deletable", "file1.txt");
        verifier.VerifyFileContent(db, "$/DelRecoverTest/Deletable", "file1.txt", 4,
            "final after file recovery\n");

        verifier.VerifyFileExists(db, "$/DelRecoverTest/Deletable/Inner", "inner.txt");
        verifier.VerifyFileContent(db, "$/DelRecoverTest/Deletable/Inner", "inner.txt", 3,
            "inner after recovery\n");

        verifier.VerifyFileExists(db, "$/DelRecoverTest", "root.txt");
        verifier.VerifyFileContent(db, "$/DelRecoverTest", "root.txt", 2,
            "root edited while Deletable is deleted\n");

        verifier.PrintDatabaseSummary(db);
    }
}
