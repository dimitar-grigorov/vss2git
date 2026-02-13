using System.Linq;
using FluentAssertions;
using Hpdi.Vss2Git.IntegrationTests.Helpers;

namespace Hpdi.Vss2Git.IntegrationTests.Tests;

/// <summary>
/// Integration tests for Scenario04_PinsAndLabels.
/// </summary>
public class PinAndLabelTests : IDisposable
{
    private readonly MigrationTestRunner _runner = new();

    public PinAndLabelTests()
    {
        _runner.Run("04_PinsAndLabels");
    }

    [Fact]
    public void Migration_FinalContentAfterPinUnpin()
    {
        var inspector = _runner.Inspector!;

        inspector.FileExists("PinTest/data.txt").Should().BeTrue();
        inspector.FileExists("PinTest/notes.txt").Should().BeTrue();

        inspector.GetFileContent("PinTest/data.txt")
            .Should().Contain("version 4", "v4 after unpin + edit");
        inspector.GetFileContent("PinTest/data.txt")
            .Should().NotContain("version 1", "v1 was superseded");
    }

    [Fact]
    public void Migration_MultipleLabelsProduceTags()
    {
        var tags = _runner.Inspector!.GetTags();

        tags.Should().HaveCountGreaterThanOrEqualTo(4);
        tags.Should().Contain(t => t.Contains("v3") || t.Contains("3_0"));
        tags.Should().Contain(t => t.Contains("v4") || t.Contains("4_0"));
        tags.Should().Contain(t => t.Contains("release") && t.Contains("candidate"));
        tags.Should().Contain(t => t.Contains("final"));
    }

    [Fact]
    public void Migration_ExactFileList()
    {
        _runner.Inspector!.GetFileList().Should().HaveCount(2, "data.txt + notes.txt");
    }

    [Fact]
    public void Migration_CommitQuality()
    {
        var commits = _runner.Inspector!.GetCommits();

        commits.Should().HaveCountGreaterThanOrEqualTo(4);

        // All ops in this scenario have explicit comments
        var withMessage = commits.Count(c => !string.IsNullOrWhiteSpace(c.Subject));
        withMessage.Should().Be(commits.Count, "all ops have VSS comments");
    }

    public void Dispose() => _runner.Dispose();
}
