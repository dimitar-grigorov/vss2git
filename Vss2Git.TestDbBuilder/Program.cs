using System.Text;
using Hpdi.Vss2Git.TestDbBuilder;
using Hpdi.Vss2Git.TestDbBuilder.Scenarios;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

const string DefaultVssInstallDir = @"C:\Program Files (x86)\Microsoft Visual SourceSafe";

var vssInstallDir = args.Length > 0 ? args[0] : DefaultVssInstallDir;
var outputBaseDir = args.Length > 1
    ? args[1]
    : Path.Combine(FindSolutionRoot(), "Vss2Git.IntegrationTests", "TestData");

// Handle "clean" command
if (args.Length > 0 && args[0].Equals("clean", StringComparison.OrdinalIgnoreCase))
{
    if (Directory.Exists(outputBaseDir))
    {
        ForceDeleteDirectory(outputBaseDir);
        Console.WriteLine($"Cleaned: {outputBaseDir}");
    }
    else
    {
        Console.WriteLine("Nothing to clean.");
    }
    return 0;
}

Console.WriteLine($"VSS Install: {vssInstallDir}");
Console.WriteLine($"Output Dir:  {outputBaseDir}");
Console.WriteLine();

var scenarios = new ITestScenario[]
{
    new Scenario01_Basic(),
    new Scenario02_RenamesAndMoves(),
    new Scenario03_SharingAndBranching(),
    new Scenario04_PinsAndLabels(),
    new Scenario05_DeleteAndRecover(),
    new Scenario06_DateRangeMigration(),
    new Scenario07_SharedFileDeleteRecover(),
    new Scenario08_ProjectMoveChain(),
    new Scenario09_DeleteRecoverProject(),
};

var passed = 0;
var failed = 0;
var errors = new List<(string scenario, string error)>();

foreach (var scenario in scenarios)
{
    Console.WriteLine($"=== {scenario.Name}: {scenario.Description} ===");

    var scenarioDir = Path.Combine(outputBaseDir, scenario.Name);
    var dbDir = Path.Combine(scenarioDir, "vss_db");
    var workDir = Path.Combine(scenarioDir, "work");

    try
    {
        if (Directory.Exists(scenarioDir))
            ForceDeleteDirectory(scenarioDir);
        Directory.CreateDirectory(scenarioDir);
        Directory.CreateDirectory(workDir);

        Console.WriteLine("Building...");
        using (var runner = new VssCommandRunner(vssInstallDir, dbDir, workDir))
        {
            runner.CreateDatabase();
            scenario.Build(runner);
        }

        Console.WriteLine("Verifying...");
        var verifier = new VssTestDatabaseVerifier(dbDir);
        scenario.Verify(verifier);

        Console.WriteLine($"=== {scenario.Name}: PASSED ===");
        Console.WriteLine();
        passed++;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"=== {scenario.Name}: FAILED ===");
        Console.WriteLine($"  Error: {ex.Message}");
        if (ex.InnerException != null)
            Console.WriteLine($"  Inner: {ex.InnerException.Message}");
        Console.WriteLine();
        failed++;
        errors.Add((scenario.Name, ex.Message));
    }
}

Console.WriteLine("========================================");
Console.WriteLine($"Results: {passed} passed, {failed} failed, {scenarios.Length} total");
if (errors.Count > 0)
{
    Console.WriteLine("\nFailures:");
    foreach (var (scenario, error) in errors)
        Console.WriteLine($"  {scenario}: {error}");
}
Console.WriteLine("========================================");

return failed > 0 ? 1 : 0;

static void ForceDeleteDirectory(string path)
{
    foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
    {
        var attrs = File.GetAttributes(file);
        if ((attrs & FileAttributes.ReadOnly) != 0)
            File.SetAttributes(file, attrs & ~FileAttributes.ReadOnly);
    }
    Directory.Delete(path, true);
}

static string FindSolutionRoot()
{
    var dir = AppContext.BaseDirectory;
    while (dir != null)
    {
        if (File.Exists(Path.Combine(dir, "Vss2Git.sln")))
            return dir;
        dir = Path.GetDirectoryName(dir);
    }
    return Directory.GetCurrentDirectory();
}
