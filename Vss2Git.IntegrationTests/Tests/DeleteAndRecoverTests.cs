using FluentAssertions;
using Hpdi.Vss2Git.IntegrationTests.Helpers;

namespace Hpdi.Vss2Git.IntegrationTests.Tests;

/// <summary>
/// Integration tests for Scenario05_DeleteAndRecover.
/// </summary>
public class DeleteAndRecoverTests : IDisposable
{
    private readonly MigrationTestRunner _runner = new();

    public DeleteAndRecoverTests()
    {
        _runner.Run("05_DeleteAndRecover");
    }

    [Fact]
    public void Migration_SurvivingFilesIntact()
    {
        var inspector = _runner.Inspector!;

        inspector.FileExists("DelTest/root.txt").Should().BeTrue();
        inspector.DirectoryExists("DelTest/KeepMe").Should().BeTrue();
        inspector.GetFileContent("DelTest/KeepMe/kept.txt")
            .Should().Contain("Still here after other deletions");
    }

    [Fact]
    public void Migration_DestroyedItemsRemoved()
    {
        var inspector = _runner.Inspector!;

        inspector.DirectoryExists("DelTest/ToDestroy").Should().BeFalse(
            "destroyed project should not exist in git");
        inspector.FileExists("DelTest/ToDestroy/destroyed.txt").Should().BeFalse(
            "destroyed file should not exist in git");
    }

    [Fact]
    [Trait("Bug", "H2")]
    public void Migration_DeletedProjectFilesShouldBeRemoved()
    {
        // BUG H2: project-level soft-delete does not remove contained files from git.
        // This test asserts the CORRECT expected behavior — it will fail until H2 is fixed.
        _runner.Inspector!.FileExists("DelTest/ToDelete/also-delete.txt").Should().BeFalse(
            "also-delete.txt should not exist after project deletion");
    }

    public void Dispose() => _runner.Dispose();
}
