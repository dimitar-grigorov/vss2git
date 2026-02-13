namespace Hpdi.Vss2Git.TestDbBuilder.Scenarios;

/// <summary>
/// Stress-tests same-timestamp revisions. Uses zero delay so operations
/// within each group share a VSS timestamp, forcing the ordering logic
/// in GitExporter.GetActionPriority to produce correct causal order.
/// </summary>
public class Scenario12_TimestampCollision : ITestScenario
{
    public string Name => "12_TimestampCollision";
    public string Description => "Same-timestamp operations: share+branch, rapid adds, move";

    public void Build(VssCommandRunner runner)
    {
        // Setup with normal delays
        runner.CreateProject("$/Rapid", "Root");
        runner.CreateProject("$/Rapid/Alpha", "Source project");
        runner.CreateProject("$/Rapid/Beta", "Share target");
        runner.CreateProject("$/Rapid/Gamma", "Move target");
        runner.CreateProject("$/Rapid/Staging", "Will be moved");

        runner.CreateAndAddFile("$/Rapid/Alpha", "shared.txt",
            "shared v1\n", "Add shared file");

        runner.CreateAndAddFile("$/Rapid/Staging", "cargo.txt",
            "cargo v1\n", "Add cargo file");

        // --- Group 1: Share + edit at same timestamp ---
        // Share creates paired actions (Share on Alpha + Add on Beta) at same time.
        // The edit that follows should still work even if all three end up
        // in the same changeset.
        var savedDelay = runner.DelayAfterRevision;
        runner.DelayAfterRevision = TimeSpan.Zero;

        runner.Share("$/Rapid/Alpha/shared.txt", "$/Rapid/Beta", "Share to Beta");
        runner.EditFile("$/Rapid/Alpha", "shared.txt",
            "shared v2 - after share\n", "Edit after share");

        runner.DelayAfterRevision = savedDelay;

        // --- Group 2: Branch after share (must come after Share in replay) ---
        runner.DelayAfterRevision = TimeSpan.Zero;

        runner.Branch("$/Rapid/Beta/shared.txt");
        runner.EditFile("$/Rapid/Beta", "shared.txt",
            "shared - Beta independent\n", "Beta independent edit");

        runner.DelayAfterRevision = savedDelay;

        // Edit Alpha again to confirm independence
        runner.EditFile("$/Rapid/Alpha", "shared.txt",
            "shared v3 - Alpha only\n", "Alpha v3");

        // --- Group 3: Rapid multiple adds (same timestamp) ---
        runner.DelayAfterRevision = TimeSpan.Zero;

        runner.CreateAndAddFile("$/Rapid/Alpha", "rapid1.txt", "rapid 1\n", "Rapid add 1");
        runner.CreateAndAddFile("$/Rapid/Alpha", "rapid2.txt", "rapid 2\n", "Rapid add 2");
        runner.CreateAndAddFile("$/Rapid/Alpha", "rapid3.txt", "rapid 3\n", "Rapid add 3");

        runner.DelayAfterRevision = savedDelay;

        // --- Group 4: Move at same timestamp ---
        // Move creates MoveFrom (on Gamma) + MoveTo (on Rapid) at same time.
        runner.Move("$/Rapid/Staging", "$/Rapid/Gamma");

        // Edit file in moved project
        runner.EditFile("$/Rapid/Gamma/Staging", "cargo.txt",
            "cargo v2 - after move\n", "Edit after move");
    }

    public void Verify(VssTestDatabaseVerifier verifier)
    {
        var db = verifier.OpenDatabase();

        verifier.VerifyProjectExists(db, "$/Rapid/Alpha");
        verifier.VerifyProjectExists(db, "$/Rapid/Beta");
        verifier.VerifyProjectExists(db, "$/Rapid/Gamma");
        verifier.VerifyProjectExists(db, "$/Rapid/Gamma/Staging");

        // Alpha should have v3
        verifier.VerifyFileExists(db, "$/Rapid/Alpha", "shared.txt");

        // Beta should have independent edit (branched)
        verifier.VerifyFileExists(db, "$/Rapid/Beta", "shared.txt");

        // Rapid adds
        verifier.VerifyFileExists(db, "$/Rapid/Alpha", "rapid1.txt");
        verifier.VerifyFileExists(db, "$/Rapid/Alpha", "rapid2.txt");
        verifier.VerifyFileExists(db, "$/Rapid/Alpha", "rapid3.txt");

        // Moved project
        verifier.VerifyFileExists(db, "$/Rapid/Gamma/Staging", "cargo.txt");

        // Alpha: shared.txt + 3 rapid files = 4
        verifier.VerifyFileCount(db, "$/Rapid/Alpha", 4);
        // Beta: shared.txt = 1
        verifier.VerifyFileCount(db, "$/Rapid/Beta", 1);

        verifier.PrintDatabaseSummary(db);
    }
}
