using System.Linq;
using FluentAssertions;
using Hpdi.Vss2Git.IntegrationTests.Helpers;

namespace Hpdi.Vss2Git.IntegrationTests.Tests;

/// <summary>
/// Integration tests for Scenario09_DeleteRecoverProject.
/// Tests project delete/recover with nested subprojects and file delete/recover.
/// </summary>
public class DeleteRecoverProjectTests : IDisposable
{
    private readonly MigrationTestRunner _runner = new();

    public DeleteRecoverProjectTests()
    {
        _runner.Run("09_DeleteRecoverProject");
    }

    [Fact]
    public void Migration_FinalFileContent()
    {
        var inspector = _runner.Inspector!;

        inspector.FileExists("DelRecoverTest/Deletable/file1.txt").Should().BeTrue();
        inspector.GetFileContent("DelRecoverTest/Deletable/file1.txt")
            .Should().Contain("final after file recovery");
    }

    [Fact]
    public void Migration_NestedSubprojectRestored()
    {
        var inspector = _runner.Inspector!;

        inspector.DirectoryExists("DelRecoverTest/Deletable/Inner").Should().BeTrue(
            "nested Inner project should be restored after project recovery");
        inspector.FileExists("DelRecoverTest/Deletable/Inner/inner.txt").Should().BeTrue();
        inspector.GetFileContent("DelRecoverTest/Deletable/Inner/inner.txt")
            .Should().Contain("inner after recovery");
    }

    [Fact]
    public void Migration_RootFilePreserved()
    {
        var inspector = _runner.Inspector!;

        inspector.FileExists("DelRecoverTest/root.txt").Should().BeTrue();
        inspector.GetFileContent("DelRecoverTest/root.txt")
            .Should().Contain("root edited while Deletable is deleted");
    }

    [Fact]
    public void Migration_ExactFileList()
    {
        var files = _runner.Inspector!.GetFileList();

        // file1.txt + inner.txt + root.txt
        files.Should().HaveCount(3);
    }

    [Fact]
    public void Migration_CommitQuality()
    {
        var commits = _runner.Inspector!.GetCommits();

        // Operations: add file1, add inner, add root, edit file1, edit inner,
        // delete project, edit root, recover project, edit file1, edit inner,
        // delete file1, recover file1, edit file1 = ~13 commits
        commits.Should().HaveCountGreaterThanOrEqualTo(10);

        var withMessage = commits.Count(c => !string.IsNullOrWhiteSpace(c.Subject));
        withMessage.Should().BeGreaterThanOrEqualTo(6);
    }

    public void Dispose() => _runner.Dispose();
}
