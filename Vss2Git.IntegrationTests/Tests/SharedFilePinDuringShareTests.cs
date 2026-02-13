using System.Linq;
using FluentAssertions;
using Hpdi.Vss2Git.IntegrationTests.Helpers;

namespace Hpdi.Vss2Git.IntegrationTests.Tests;

/// <summary>
/// Tests shared file pin/unpin across projects (Scenario 10).
/// Key: after unpin with no subsequent edit, B should have current version.
/// </summary>
public class SharedFilePinDuringShareTests : IDisposable
{
    private readonly MigrationTestRunner _runner = new();

    public SharedFilePinDuringShareTests()
    {
        _runner.Run("10_SharedFilePinDuringShare");
    }

    [Fact]
    public void Migration_ProjectAHasLatestVersion()
    {
        var inspector = _runner.Inspector!;

        inspector.FileExists("PinShare/ProjectA/data.txt").Should().BeTrue();
        inspector.GetFileContent("PinShare/ProjectA/data.txt")
            .Should().Contain("version 4");
    }

    [Fact]
    public void Migration_ProjectBHasCurrentVersionAfterUnpin()
    {
        var inspector = _runner.Inspector!;

        inspector.FileExists("PinShare/ProjectB/data.txt").Should().BeTrue();
        inspector.GetFileContent("PinShare/ProjectB/data.txt")
            .Should().Contain("version 4",
                "after unpin, B should have the current version, not the stale pinned v2");
    }

    [Fact]
    public void Migration_ProjectCHasLatestVersion()
    {
        var inspector = _runner.Inspector!;

        inspector.FileExists("PinShare/ProjectC/data.txt").Should().BeTrue();
        inspector.GetFileContent("PinShare/ProjectC/data.txt")
            .Should().Contain("version 4");
    }

    [Fact]
    public void Migration_AllThreeProjectsHaveSameContent()
    {
        var inspector = _runner.Inspector!;

        var contentA = inspector.GetFileContent("PinShare/ProjectA/data.txt");
        var contentB = inspector.GetFileContent("PinShare/ProjectB/data.txt");
        var contentC = inspector.GetFileContent("PinShare/ProjectC/data.txt");

        contentA.Should().Be(contentB, "A and B should match after unpin");
        contentA.Should().Be(contentC, "A and C should match (always shared)");
    }

    [Fact]
    public void Migration_SoloFileOnlyInProjectA()
    {
        var inspector = _runner.Inspector!;

        inspector.FileExists("PinShare/ProjectA/solo.txt").Should().BeTrue();
        inspector.FileExists("PinShare/ProjectB/solo.txt").Should().BeFalse();
        inspector.FileExists("PinShare/ProjectC/solo.txt").Should().BeFalse();
    }

    [Fact]
    public void Migration_ExactFileList()
    {
        var files = _runner.Inspector!.GetFileList();

        // data.txt in A, B, C (3) + solo.txt in A (1) = 4
        files.Should().HaveCount(4);
    }

    [Fact]
    public void Migration_CommitQuality()
    {
        var commits = _runner.Inspector!.GetCommits();

        // Operations: add data, share B, share C, edit v2, pin B (no-op),
        // edit v3, edit v4, unpin B + add solo = ~7 commits
        commits.Should().HaveCountGreaterThanOrEqualTo(7);

        var withMessage = commits.Count(c => !string.IsNullOrWhiteSpace(c.Subject));
        withMessage.Should().BeGreaterThanOrEqualTo(5);
    }

    public void Dispose() => _runner.Dispose();
}
