namespace Hpdi.Vss2Git.TestDbBuilder.Scenarios;

/// <summary>
/// Basic scenario: add files, edit, delete, label.
/// Tests the core migration pipeline works for common operations.
/// </summary>
public class Scenario01_Basic : ITestScenario
{
    public string Name => "01_Basic";
    public string Description => "Add files, edit, delete, label";

    public void Build(VssCommandRunner runner)
    {
        // Create project structure
        runner.CreateProject("$/TestProject", "Create test project");
        runner.CreateProject("$/TestProject/SubFolder", "Create sub-folder");

        // Add files to root project
        runner.CreateAndAddFile("$/TestProject", "readme.txt",
            "This is the readme file.\nVersion 1.\n",
            "Initial add of readme");

        runner.CreateAndAddFile("$/TestProject", "main.c",
            "#include <stdio.h>\nint main() { return 0; }\n",
            "Initial add of main.c");

        // Add file to sub-folder
        runner.CreateAndAddFile("$/TestProject/SubFolder", "helper.h",
            "#ifndef HELPER_H\n#define HELPER_H\nvoid help();\n#endif\n",
            "Add helper header");

        // Edit readme.txt twice (creates versions 2 and 3)
        runner.EditFile("$/TestProject", "readme.txt",
            "This is the readme file.\nVersion 2 - updated.\nNew line added.\n",
            "Update readme to version 2");

        runner.EditFile("$/TestProject", "main.c",
            "#include <stdio.h>\n#include \"helper.h\"\nint main() {\n  help();\n  return 0;\n}\n",
            "Include helper in main");

        runner.EditFile("$/TestProject", "readme.txt",
            "This is the readme file.\nVersion 3 - final.\nNew line added.\nAnother line.\n",
            "Final readme update");

        // Delete a file
        runner.Delete("$/TestProject/SubFolder/helper.h");

        // Apply a label
        runner.Label("$/TestProject", "v1.0", "Release version 1.0");

        // Add file after label to verify label doesn't include it
        runner.CreateAndAddFile("$/TestProject", "config.ini",
            "[settings]\nverbose=true\n",
            "Add config file after label");
    }

    public void Verify(VssTestDatabaseVerifier verifier)
    {
        var db = verifier.OpenDatabase();

        // Verify project structure
        verifier.VerifyProjectExists(db, "$/TestProject");
        verifier.VerifyProjectExists(db, "$/TestProject/SubFolder");

        // Verify files exist
        verifier.VerifyFileExists(db, "$/TestProject", "readme.txt");
        verifier.VerifyFileExists(db, "$/TestProject", "main.c");
        verifier.VerifyFileExists(db, "$/TestProject", "config.ini");

        // Verify revision counts: readme=3 (add+2 edits), main=2 (add+edit), config=1 (add)
        verifier.VerifyFileRevisionCount(db, "$/TestProject", "readme.txt", 3);
        verifier.VerifyFileRevisionCount(db, "$/TestProject", "main.c", 2);
        verifier.VerifyFileRevisionCount(db, "$/TestProject", "config.ini", 1);

        // Verify latest content
        verifier.VerifyFileContent(db, "$/TestProject", "readme.txt", 3,
            "This is the readme file.\nVersion 3 - final.\nNew line added.\nAnother line.\n");

        verifier.VerifyFileContent(db, "$/TestProject", "main.c", 2,
            "#include <stdio.h>\n#include \"helper.h\"\nint main() {\n  help();\n  return 0;\n}\n");

        // Verify historical content (version 1)
        verifier.VerifyFileContent(db, "$/TestProject", "readme.txt", 1,
            "This is the readme file.\nVersion 1.\n");

        // File count: helper.h is deleted but readme.txt + main.c + config.ini = 3
        verifier.VerifyFileCount(db, "$/TestProject", 3);

        verifier.PrintDatabaseSummary(db);
    }
}
