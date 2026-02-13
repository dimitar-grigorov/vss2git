namespace Hpdi.Vss2Git.TestDbBuilder.Scenarios;

/// <summary>
/// Tests file sharing and branching.
/// Targets bug H4 (shared file version tracking is per-file, not per-project).
/// </summary>
public class Scenario03_SharingAndBranching : ITestScenario
{
    public string Name => "03_SharingAndBranching";
    public string Description => "Share file, edit shared, branch, edit after branch";

    public void Build(VssCommandRunner runner)
    {
        // Create three projects for sharing tests
        runner.CreateProject("$/ProjectA", "Project A");
        runner.CreateProject("$/ProjectB", "Project B");
        runner.CreateProject("$/ProjectC", "Project C");

        // Add a file to ProjectA
        runner.CreateAndAddFile("$/ProjectA", "shared.txt",
            "Shared file content - version 1.\n",
            "Add shared file");

        // Share from ProjectA to ProjectB — both now point to same physical file
        runner.Share("$/ProjectA/shared.txt", "$/ProjectB", "Share to B");

        // Edit shared file from ProjectA — both A and B should see this change
        runner.EditFile("$/ProjectA", "shared.txt",
            "Shared file content - version 2 (edited from A).\n",
            "Edit shared from A");

        // Share to ProjectC too (now A, B, C all share the file)
        runner.Share("$/ProjectA/shared.txt", "$/ProjectC", "Share to C");

        // Branch the file in ProjectB — makes an independent copy
        runner.Branch("$/ProjectB/shared.txt");

        // Edit from ProjectA — only A and C see this change, B is now independent
        runner.EditFile("$/ProjectA", "shared.txt",
            "Shared file content - version 3 (after branch).\n",
            "Edit after branch");

        // Edit the branched copy in ProjectB independently
        runner.EditFile("$/ProjectB", "shared.txt",
            "Branched file in B - independent edit.\n",
            "Independent edit in B");

        // Add a non-shared file for comparison
        runner.CreateAndAddFile("$/ProjectA", "unique.txt",
            "This file is only in ProjectA.\n",
            "Add unique file");
    }

    public void Verify(VssTestDatabaseVerifier verifier)
    {
        var db = verifier.OpenDatabase();

        // Verify all projects exist
        verifier.VerifyProjectExists(db, "$/ProjectA");
        verifier.VerifyProjectExists(db, "$/ProjectB");
        verifier.VerifyProjectExists(db, "$/ProjectC");

        // Verify shared file exists in all three projects
        verifier.VerifyFileExists(db, "$/ProjectA", "shared.txt");
        verifier.VerifyFileExists(db, "$/ProjectB", "shared.txt");
        verifier.VerifyFileExists(db, "$/ProjectC", "shared.txt");

        // ProjectA has version 3 (latest shared edit)
        verifier.VerifyFileContent(db, "$/ProjectA", "shared.txt", 3,
            "Shared file content - version 3 (after branch).\n");

        // ProjectB branched file: v1=add, v2=shared edit, v3=branch point, v4=independent edit
        verifier.VerifyFileRevisionCount(db, "$/ProjectB", "shared.txt", 4);
        verifier.VerifyFileContent(db, "$/ProjectB", "shared.txt", 4,
            "Branched file in B - independent edit.\n");

        // Verify unique file only in ProjectA
        verifier.VerifyFileExists(db, "$/ProjectA", "unique.txt");
        verifier.VerifyFileCount(db, "$/ProjectA", 2);  // shared.txt + unique.txt

        verifier.PrintDatabaseSummary(db);
    }
}
