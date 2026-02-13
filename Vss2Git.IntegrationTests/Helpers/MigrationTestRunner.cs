using System;
using System.IO;
using System.Text;

namespace Hpdi.Vss2Git.IntegrationTests.Helpers;

/// <summary>
/// Runs the full VSS → Git migration pipeline for integration tests.
/// Creates a temp git directory, runs MigrationOrchestrator, waits for completion,
/// and provides a GitRepoInspector for assertions.
/// </summary>
public class MigrationTestRunner : IDisposable
{
    private string? _gitDir;
    private bool _disposed;

    public TestUserInteraction UserInteraction { get; } = new();
    public GitRepoInspector? Inspector { get; private set; }
    public string? GitDirectory => _gitDir;

    static MigrationTestRunner()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>
    /// Runs the full migration pipeline for a given scenario.
    /// </summary>
    /// <param name="scenarioName">Scenario folder name (e.g. "01_Basic")</param>
    /// <param name="vssProject">VSS project path to migrate (default: root "$")</param>
    /// <param name="configureAction">Optional action to customize MigrationConfiguration</param>
    public void Run(string scenarioName, string vssProject = "$",
        Action<MigrationConfiguration>? configureAction = null)
    {
        var vssDir = GetTestDataPath(scenarioName);
        if (!Directory.Exists(vssDir))
        {
            throw new DirectoryNotFoundException(
                $"VSS test database not found at: {vssDir}. Run TestDbBuilder first.");
        }

        _gitDir = Path.Combine(Path.GetTempPath(), "vss2git_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_gitDir);

        var config = new MigrationConfiguration
        {
            VssDirectory = vssDir,
            VssProject = vssProject,
            GitDirectory = _gitDir,
            DefaultEmailDomain = "test.local",
            VssEncoding = Encoding.Default,
            IgnoreErrors = true,
            ForceAnnotatedTags = true,
            TranscodeComments = true,
            DefaultComment = "",
            AnyCommentSeconds = 30,
            SameCommentSeconds = 600
        };

        configureAction?.Invoke(config);

        var workQueue = new WorkQueue(1);
        var statusReporter = new TestStatusReporter();
        var orchestrator = new MigrationOrchestrator(config, workQueue, UserInteraction, statusReporter);

        var started = orchestrator.Run();
        if (!started)
        {
            var errors = string.Join("; ", UserInteraction.FatalErrors);
            throw new InvalidOperationException($"Migration failed to start: {errors}");
        }

        // Wait for the work queue to complete all work
        workQueue.WaitIdle();

        // Check for exceptions during migration
        var exceptions = workQueue.FetchExceptions();
        if (exceptions != null && exceptions.Count > 0)
        {
            throw new AggregateException("Exceptions during migration", exceptions);
        }

        Inspector = new GitRepoInspector(_gitDir);
    }

    private static string GetTestDataPath(string scenarioName)
    {
        // Walk up from bin/Debug/net8.0 to project directory
        var assemblyDir = AppContext.BaseDirectory;
        var projectDir = Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", ".."));
        return Path.Combine(projectDir, "TestData", scenarioName, "vss_db");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_gitDir != null && Directory.Exists(_gitDir))
        {
            try
            {
                // Remove read-only attributes (git objects are read-only)
                foreach (var file in Directory.EnumerateFiles(_gitDir, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }
                Directory.Delete(_gitDir, true);
            }
            catch
            {
                // Best-effort cleanup
            }
        }
    }
}
