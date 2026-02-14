using System.Linq;
using FluentAssertions;
using Hpdi.Vss2Git.IntegrationTests.Helpers;

namespace Hpdi.Vss2Git.IntegrationTests.Tests;

/// <summary>
/// Same-timestamp operations stress test (Scenario 12).
/// Verifies causal ordering in GitExporter.GetActionPriority.
/// </summary>
public class TimestampCollisionTests : IDisposable
{
    private readonly MigrationTestRunner _runner = new();

    public TimestampCollisionTests()
    {
        _runner.Run("12_TimestampCollision");
    }

    [Fact]
    public void Migration_SharedFileInBothProjects()
    {
        var inspector = _runner.Inspector!;

        inspector.FileExists("Rapid/Alpha/shared.txt").Should().BeTrue();
        inspector.FileExists("Rapid/Beta/shared.txt").Should().BeTrue();
    }

    [Fact]
    public void Migration_BranchIsolation()
    {
        var inspector = _runner.Inspector!;

        // Alpha has v3, Beta has independent edit
        inspector.GetFileContent("Rapid/Alpha/shared.txt")
            .Should().Contain("Alpha only");
        inspector.GetFileContent("Rapid/Beta/shared.txt")
            .Should().Contain("Beta independent");
    }

    [Fact]
    public void Migration_RapidAddsAllPresent()
    {
        var inspector = _runner.Inspector!;

        inspector.FileExists("Rapid/Alpha/rapid1.txt").Should().BeTrue();
        inspector.FileExists("Rapid/Alpha/rapid2.txt").Should().BeTrue();
        inspector.FileExists("Rapid/Alpha/rapid3.txt").Should().BeTrue();
    }

    [Fact]
    public void Migration_MovedProjectIntact()
    {
        var inspector = _runner.Inspector!;

        inspector.FileExists("Rapid/Gamma/Staging/cargo.txt").Should().BeTrue();
        inspector.GetFileContent("Rapid/Gamma/Staging/cargo.txt")
            .Should().Contain("after move");
    }

    [Fact]
    public void Migration_FileCountCorrect()
    {
        var files = _runner.Inspector!.GetFileList();

        // Alpha: shared.txt + rapid1-3 = 4
        // Beta: shared.txt = 1
        // Gamma/Staging: cargo.txt = 1
        // Total = 6
        files.Should().HaveCount(6);
    }

    [Fact]
    public void Migration_CommitQuality()
    {
        var commits = _runner.Inspector!.GetCommits();

        commits.Should().HaveCount(6);

        var withMessage = commits.Count(c => !string.IsNullOrWhiteSpace(c.Subject));
        withMessage.Should().Be(6);
    }

    public void Dispose() => _runner.Dispose();
}
