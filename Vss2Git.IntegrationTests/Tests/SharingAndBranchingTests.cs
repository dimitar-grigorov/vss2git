using FluentAssertions;
using Hpdi.Vss2Git.IntegrationTests.Helpers;

namespace Hpdi.Vss2Git.IntegrationTests.Tests;

/// <summary>
/// Integration tests for Scenario03_SharingAndBranching.
/// </summary>
public class SharingAndBranchingTests : IDisposable
{
    private readonly MigrationTestRunner _runner = new();

    public SharingAndBranchingTests()
    {
        _runner.Run("03_SharingAndBranching");
    }

    [Fact]
    public void Migration_SharedFileInAllProjects()
    {
        var inspector = _runner.Inspector!;

        inspector.FileExists("ProjectA/shared.txt").Should().BeTrue();
        inspector.FileExists("ProjectB/shared.txt").Should().BeTrue();
        inspector.FileExists("ProjectC/shared.txt").Should().BeTrue();
    }

    [Fact]
    public void Migration_SharedEditPropagation()
    {
        var inspector = _runner.Inspector!;

        // A and C still share — both should have version 3
        inspector.GetFileContent("ProjectA/shared.txt").Should().Contain("version 3");
        inspector.GetFileContent("ProjectC/shared.txt").Should().Contain("version 3");
    }

    [Fact]
    public void Migration_BranchIsolation()
    {
        // B branched and edited independently — should NOT have version 3
        var content = _runner.Inspector!.GetFileContent("ProjectB/shared.txt");
        content.Should().Contain("independent edit");
    }

    [Fact]
    public void Migration_UniqueFileNotShared()
    {
        var inspector = _runner.Inspector!;

        inspector.FileExists("ProjectA/unique.txt").Should().BeTrue();
        inspector.FileExists("ProjectB/unique.txt").Should().BeFalse();
        inspector.FileExists("ProjectC/unique.txt").Should().BeFalse();
    }

    public void Dispose() => _runner.Dispose();
}
