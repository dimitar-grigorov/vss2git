namespace Hpdi.Vss2Git.TestDbBuilder.Scenarios;

/// <summary>
/// Tests date-range (chunked) migration by creating three distinct phases
/// separated by Thread.Sleep delays, ensuring each phase gets different
/// VSS timestamps for date-range filtering.
/// </summary>
public class Scenario06_DateRangeMigration : ITestScenario
{
    public string Name => "06_DateRangeMigration";
    public string Description => "Three-phase operations with time gaps for chunked migration";

    public void Build(VssCommandRunner runner)
    {
        // Disable auto-delay: this scenario needs operations within each phase to share
        // the same timestamp (grouped into one changeset), with only the manual Thread.Sleep
        // gaps between phases providing time separation for date-range filtering.
        runner.DelayAfterRevision = TimeSpan.Zero;

        // === PHASE 1: Initial setup ===
        runner.CreateProject("$/ChunkTest", "Phase1: Create project");

        runner.CreateAndAddFile("$/ChunkTest", "file1.txt",
            "File 1 - Phase 1 content.\n",
            "Phase1: Add file1");

        runner.CreateAndAddFile("$/ChunkTest", "file2.txt",
            "File 2 - Phase 1 content.\n",
            "Phase1: Add file2");

        runner.Label("$/ChunkTest", "Phase1_Release", "End of phase 1");

        // === TIME GAP: 2 seconds between phases for date-range boundary detection ===
        Console.WriteLine("  Sleeping 2s between Phase 1 and Phase 2...");
        Thread.Sleep(2000);

        // === PHASE 2: Edits and additions ===
        runner.EditFile("$/ChunkTest", "file1.txt",
            "File 1 - Phase 2 updated content.\n",
            "Phase2: Edit file1");

        runner.CreateAndAddFile("$/ChunkTest", "file3.txt",
            "File 3 - Phase 2 content.\n",
            "Phase2: Add file3");

        runner.Label("$/ChunkTest", "Phase2_Release", "End of phase 2");

        // === TIME GAP: 2 seconds between phases ===
        Console.WriteLine("  Sleeping 2s between Phase 2 and Phase 3...");
        Thread.Sleep(2000);

        // === PHASE 3: More edits, delete, new file ===
        runner.EditFile("$/ChunkTest", "file2.txt",
            "File 2 - Phase 3 updated content.\n",
            "Phase3: Edit file2");

        runner.Delete("$/ChunkTest/file3.txt");

        runner.CreateAndAddFile("$/ChunkTest", "file4.txt",
            "File 4 - Phase 3 content.\n",
            "Phase3: Add file4");

        runner.Label("$/ChunkTest", "Phase3_Release", "End of phase 3");
    }

    public void Verify(VssTestDatabaseVerifier verifier)
    {
        var db = verifier.OpenDatabase();

        verifier.VerifyProjectExists(db, "$/ChunkTest");

        // file1: 2 versions (add + edit in phase 2)
        verifier.VerifyFileRevisionCount(db, "$/ChunkTest", "file1.txt", 2);
        verifier.VerifyFileContent(db, "$/ChunkTest", "file1.txt", 1,
            "File 1 - Phase 1 content.\n");
        verifier.VerifyFileContent(db, "$/ChunkTest", "file1.txt", 2,
            "File 1 - Phase 2 updated content.\n");

        // file2: 2 versions (add + edit in phase 3)
        verifier.VerifyFileRevisionCount(db, "$/ChunkTest", "file2.txt", 2);
        verifier.VerifyFileContent(db, "$/ChunkTest", "file2.txt", 1,
            "File 2 - Phase 1 content.\n");
        verifier.VerifyFileContent(db, "$/ChunkTest", "file2.txt", 2,
            "File 2 - Phase 3 updated content.\n");

        // file4: 1 version (add in phase 3)
        verifier.VerifyFileRevisionCount(db, "$/ChunkTest", "file4.txt", 1);

        // file3 is soft-deleted but still in VSS tree, total = 4
        verifier.VerifyFileCount(db, "$/ChunkTest", 4);

        verifier.PrintDatabaseSummary(db);
    }
}
