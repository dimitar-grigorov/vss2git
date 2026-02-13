using System.Linq;
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

        // A and C still share — both should have v3
        inspector.GetFileContent("ProjectA/shared.txt").Should().Contain("version 3");
        inspector.GetFileContent("ProjectC/shared.txt").Should().Contain("version 3");

        var contentA = inspector.GetFileContent("ProjectA/shared.txt");
        var contentC = inspector.GetFileContent("ProjectC/shared.txt");
        contentA.Should().Be(contentC, "A and C still share the same file");
    }

    [Fact]
    public void Migration_BranchIsolation()
    {
        var contentB = _runner.Inspector!.GetFileContent("ProjectB/shared.txt");
        contentB.Should().Contain("independent edit");
        contentB.Should().NotContain("version 3", "B branched before v3 edit");
    }

    [Fact]
    public void Migration_UniqueFileNotShared()
    {
        var inspector = _runner.Inspector!;

        inspector.FileExists("ProjectA/unique.txt").Should().BeTrue();
        inspector.GetFileContent("ProjectA/unique.txt").Should().Contain("only in ProjectA");
        inspector.FileExists("ProjectB/unique.txt").Should().BeFalse();
        inspector.FileExists("ProjectC/unique.txt").Should().BeFalse();
    }

    [Fact]
    public void Migration_FileCountPerProject()
    {
        var inspector = _runner.Inspector!;

        inspector.GetFilesInDirectory("ProjectA").Should().HaveCount(2);
        inspector.GetFilesInDirectory("ProjectB").Should().HaveCount(1);
        inspector.GetFilesInDirectory("ProjectC").Should().HaveCount(1);
    }

    [Fact]
    public void Migration_CommitQuality()
    {
        var commits = _runner.Inspector!.GetCommits();

        commits.Should().HaveCountGreaterThanOrEqualTo(4);

        var withMessage = commits.Count(c => !string.IsNullOrWhiteSpace(c.Subject));
        withMessage.Should().BeGreaterThanOrEqualTo(3);
    }

    public void Dispose() => _runner.Dispose();
}
