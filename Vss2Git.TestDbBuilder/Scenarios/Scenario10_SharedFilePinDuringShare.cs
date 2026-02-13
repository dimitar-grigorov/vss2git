namespace Hpdi.Vss2Git.TestDbBuilder.Scenarios;

/// <summary>
/// Shared file pin/unpin across projects. Verifies H4/H5: after unpin,
/// the project receives the current version even without a subsequent edit.
/// </summary>
public class Scenario10_SharedFilePinDuringShare : ITestScenario
{
    public string Name => "10_SharedFilePinDuringShare";
    public string Description => "Shared file pin/unpin across projects, version desync stress test";

    public void Build(VssCommandRunner runner)
    {
        runner.CreateProject("$/PinShare", "Pin+share root");
        runner.CreateProject("$/PinShare/ProjectA", "Project A - always active");
        runner.CreateProject("$/PinShare/ProjectB", "Project B - will be pinned");
        runner.CreateProject("$/PinShare/ProjectC", "Project C - always active");

        // Add file and share to B and C
        runner.CreateAndAddFile("$/PinShare/ProjectA", "data.txt",
            "version 1 - original\n", "Add data file v1");

        runner.Share("$/PinShare/ProjectA/data.txt", "$/PinShare/ProjectB", "Share to B");
        runner.Share("$/PinShare/ProjectA/data.txt", "$/PinShare/ProjectC", "Share to C");

        // Edit v2 — all three get it
        runner.EditFile("$/PinShare/ProjectA", "data.txt",
            "version 2 - all see this\n", "Edit v2");

        // Pin B at version 2 — B frozen, A and C continue
        runner.Pin("$/PinShare/ProjectB/data.txt", 2);

        // Edit v3 — only A and C
        runner.EditFile("$/PinShare/ProjectA", "data.txt",
            "version 3 - B is pinned\n", "Edit v3 while B pinned");

        // Edit v4 — only A and C
        runner.EditFile("$/PinShare/ProjectA", "data.txt",
            "version 4 - B still pinned\n", "Edit v4 while B pinned");

        // Unpin B — B should now show version 4 (current)
        runner.Unpin("$/PinShare/ProjectB/data.txt");

        // No edit after unpin — verifies unpin writes the file itself
        runner.CreateAndAddFile("$/PinShare/ProjectA", "solo.txt",
            "solo file\n", "Add solo file");
    }

    public void Verify(VssTestDatabaseVerifier verifier)
    {
        var db = verifier.OpenDatabase();

        verifier.VerifyProjectExists(db, "$/PinShare/ProjectA");
        verifier.VerifyProjectExists(db, "$/PinShare/ProjectB");
        verifier.VerifyProjectExists(db, "$/PinShare/ProjectC");

        // A should have v4
        verifier.VerifyFileExists(db, "$/PinShare/ProjectA", "data.txt");
        verifier.VerifyFileContent(db, "$/PinShare/ProjectA", "data.txt", 4,
            "version 4 - B still pinned\n");

        // B should have v4 too (after unpin)
        verifier.VerifyFileExists(db, "$/PinShare/ProjectB", "data.txt");

        // C should have v4
        verifier.VerifyFileExists(db, "$/PinShare/ProjectC", "data.txt");
        verifier.VerifyFileContent(db, "$/PinShare/ProjectC", "data.txt", 4,
            "version 4 - B still pinned\n");

        // A has 2 files, B and C have 1
        verifier.VerifyFileCount(db, "$/PinShare/ProjectA", 2);
        verifier.VerifyFileCount(db, "$/PinShare/ProjectB", 1);
        verifier.VerifyFileCount(db, "$/PinShare/ProjectC", 1);

        verifier.PrintDatabaseSummary(db);
    }
}
