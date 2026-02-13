namespace Hpdi.Vss2Git.TestDbBuilder.Scenarios;

/// <summary>
/// Tests shared file lifecycle: share → edit → delete from one project →
/// edit while deleted → recover → edit after recovery → destroy from another.
/// Targets bug H4 (shared file version tracking desync after delete/recover).
/// </summary>
public class Scenario07_SharedFileDeleteRecover : ITestScenario
{
    public string Name => "07_SharedFileDeleteRecover";
    public string Description => "Shared file delete/recover cycle across three projects";

    public void Build(VssCommandRunner runner)
    {
        // Create three projects
        runner.CreateProject("$/ShareTest", "Share test root");
        runner.CreateProject("$/ShareTest/ProjectA", "Project A");
        runner.CreateProject("$/ShareTest/ProjectB", "Project B");
        runner.CreateProject("$/ShareTest/ProjectC", "Project C");

        // Add shared.txt to ProjectA
        runner.CreateAndAddFile("$/ShareTest/ProjectA", "shared.txt",
            "version 1 - original\n",
            "Add shared file v1");

        // Share to ProjectB and ProjectC
        runner.Share("$/ShareTest/ProjectA/shared.txt", "$/ShareTest/ProjectB",
            "Share to B");
        runner.Share("$/ShareTest/ProjectA/shared.txt", "$/ShareTest/ProjectC",
            "Share to C");

        // Edit shared file — all three should see v2
        runner.EditFile("$/ShareTest/ProjectA", "shared.txt",
            "version 2 - shared edit\n",
            "Edit shared v2");

        // Delete shared.txt from ProjectB (soft delete)
        runner.Delete("$/ShareTest/ProjectB/shared.txt");

        // Edit shared file — only A and C see v3
        runner.EditFile("$/ShareTest/ProjectA", "shared.txt",
            "version 3 - after delete from B\n",
            "Edit v3 while B deleted");

        // Recover shared.txt in ProjectB
        runner.Recover("$/ShareTest/ProjectB/shared.txt");

        // Edit shared file — all three should see v4 again
        runner.EditFile("$/ShareTest/ProjectA", "shared.txt",
            "version 4 - after recovery\n",
            "Edit v4 after recovery");

        // Destroy shared.txt from ProjectC (permanent removal)
        runner.Destroy("$/ShareTest/ProjectC/shared.txt");

        // Edit shared file — only A and B see v5
        runner.EditFile("$/ShareTest/ProjectA", "shared.txt",
            "version 5 - final\n",
            "Edit v5 final");
    }

    public void Verify(VssTestDatabaseVerifier verifier)
    {
        var db = verifier.OpenDatabase();

        verifier.VerifyProjectExists(db, "$/ShareTest/ProjectA");
        verifier.VerifyProjectExists(db, "$/ShareTest/ProjectB");
        verifier.VerifyProjectExists(db, "$/ShareTest/ProjectC");

        // A and B should have shared.txt with latest content
        verifier.VerifyFileExists(db, "$/ShareTest/ProjectA", "shared.txt");
        verifier.VerifyFileExists(db, "$/ShareTest/ProjectB", "shared.txt");

        // C's shared.txt was destroyed — verify file count is 0
        verifier.VerifyFileCount(db, "$/ShareTest/ProjectC", 0);

        // Verify latest content in A
        verifier.VerifyFileContent(db, "$/ShareTest/ProjectA", "shared.txt", 5,
            "version 5 - final\n");

        verifier.PrintDatabaseSummary(db);
    }
}
