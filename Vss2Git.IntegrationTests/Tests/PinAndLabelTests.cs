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
            .Should().Contain("version 4",
                "should have version 4 content after unpin + edit");
    }

    [Fact]
    public void Migration_MultipleLabelsProduceTags()
    {
        var tags = _runner.Inspector!.GetTags();

        // Labels: v3.0, v4.0, release-candidate, final-release
        tags.Should().Contain(t => t.Contains("v3") || t.Contains("3_0"));
        tags.Should().Contain(t => t.Contains("v4") || t.Contains("4_0"));
        tags.Should().Contain(t => t.Contains("release") && t.Contains("candidate"));
        tags.Should().Contain(t => t.Contains("final"));
    }

    public void Dispose() => _runner.Dispose();
}
