namespace Hpdi.Vss2Git.TestDbBuilder.Scenarios;

/// <summary>
/// Tests pin/unpin operations and multiple labels.
/// Targets bug H5 (pin/unpin doesn't track pinned version).
/// </summary>
public class Scenario04_PinsAndLabels : ITestScenario
{
    public string Name => "04_PinsAndLabels";
    public string Description => "Pin at version, edit, unpin, multiple labels";

    public void Build(VssCommandRunner runner)
    {
        runner.CreateProject("$/PinTest", "Pin test project");

        // Create file with multiple versions (v1, v2, v3)
        runner.CreateAndAddFile("$/PinTest", "data.txt",
            "Data version 1.\n",
            "Add data file");

        runner.EditFile("$/PinTest", "data.txt",
            "Data version 2.\n",
            "Update to v2");

        runner.EditFile("$/PinTest", "data.txt",
            "Data version 3.\n",
            "Update to v3");

        // Label at version 3
        runner.Label("$/PinTest", "v3.0", "Label at version 3");

        // Pin at version 2 — file appears as v2 content even though v3 exists
        runner.Pin("$/PinTest/data.txt", 2);

        // Editing a pinned file requires unpinning first in VSS
        runner.Unpin("$/PinTest/data.txt");

        // Edit to version 4 (after unpin)
        runner.EditFile("$/PinTest", "data.txt",
            "Data version 4 (after unpin).\n",
            "Update to v4 after unpin");

        // Label the final state
        runner.Label("$/PinTest", "v4.0", "Label at version 4");

        // Add a second file to test multiple files with labels
        runner.CreateAndAddFile("$/PinTest", "notes.txt",
            "Notes file.\n",
            "Add notes file");

        // Multiple labels on same project
        runner.Label("$/PinTest", "release-candidate", "RC label");
        runner.Label("$/PinTest", "final-release", "Final release label");
    }

    public void Verify(VssTestDatabaseVerifier verifier)
    {
        var db = verifier.OpenDatabase();

        verifier.VerifyProjectExists(db, "$/PinTest");

        // data.txt should have 4 versions: add + 3 edits (v2, v3, v4)
        verifier.VerifyFileRevisionCount(db, "$/PinTest", "data.txt", 4);

        // Verify content at each version
        verifier.VerifyFileContent(db, "$/PinTest", "data.txt", 1, "Data version 1.\n");
        verifier.VerifyFileContent(db, "$/PinTest", "data.txt", 2, "Data version 2.\n");
        verifier.VerifyFileContent(db, "$/PinTest", "data.txt", 3, "Data version 3.\n");
        verifier.VerifyFileContent(db, "$/PinTest", "data.txt", 4, "Data version 4 (after unpin).\n");

        // Verify notes.txt (single version)
        verifier.VerifyFileExists(db, "$/PinTest", "notes.txt");
        verifier.VerifyFileRevisionCount(db, "$/PinTest", "notes.txt", 1);

        // Project should have 2 files: data.txt + notes.txt
        verifier.VerifyFileCount(db, "$/PinTest", 2);

        verifier.PrintDatabaseSummary(db);
    }
}
