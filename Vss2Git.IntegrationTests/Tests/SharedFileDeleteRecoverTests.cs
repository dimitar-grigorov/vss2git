using System.Linq;
using FluentAssertions;
using Hpdi.Vss2Git.IntegrationTests.Helpers;

namespace Hpdi.Vss2Git.IntegrationTests.Tests;

/// <summary>
/// Integration tests for Scenario07_SharedFileDeleteRecover.
/// Tests shared file lifecycle: share, edit, delete, recover, destroy.
/// </summary>
public class SharedFileDeleteRecoverTests : IDisposable
{
    private readonly MigrationTestRunner _runner = new();

    public SharedFileDeleteRecoverTests()
    {
        _runner.Run("07_SharedFileDeleteRecover");
    }

    [Fact]
    public void Migration_SharedFileInProjectA()
    {
        var inspector = _runner.Inspector!;

        inspector.FileExists("ShareTest/ProjectA/shared.txt").Should().BeTrue();
        inspector.GetFileContent("ShareTest/ProjectA/shared.txt")
            .Should().Contain("version 5 - final");
    }

    [Fact]
    public void Migration_SharedFileInProjectB()
    {
        var inspector = _runner.Inspector!;

        inspector.FileExists("ShareTest/ProjectB/shared.txt").Should().BeTrue();
        inspector.GetFileContent("ShareTest/ProjectB/shared.txt")
            .Should().Contain("version 5 - final");
    }

    [Fact]
    public void Migration_DestroyedFileNotInProjectC()
    {
        _runner.Inspector!.FileExists("ShareTest/ProjectC/shared.txt")
            .Should().BeFalse("shared.txt was destroyed from ProjectC");
    }

    [Fact]
    public void Migration_ContentIdentityAB()
    {
        var inspector = _runner.Inspector!;

        var contentA = inspector.GetFileContent("ShareTest/ProjectA/shared.txt");
        var contentB = inspector.GetFileContent("ShareTest/ProjectB/shared.txt");
        contentA.Should().Be(contentB, "A and B still share the file after recovery");
    }

    [Fact]
    public void Migration_ExactFileList()
    {
        var files = _runner.Inspector!.GetFileList();

        // Only shared.txt in A and B (C was destroyed)
        files.Should().HaveCount(2, "shared.txt in ProjectA + shared.txt in ProjectB");
    }

    [Fact]
    public void Migration_CommitQuality()
    {
        var commits = _runner.Inspector!.GetCommits();

        // Operations: add, share B, share C, edit v2, delete B, edit v3,
        // recover B, edit v4, destroy C, edit v5 = ~10 commits
        commits.Should().HaveCountGreaterThanOrEqualTo(8);

        // Most edits have comments
        var withMessage = commits.Count(c => !string.IsNullOrWhiteSpace(c.Subject));
        withMessage.Should().BeGreaterThanOrEqualTo(5);
    }

    public void Dispose() => _runner.Dispose();
}
