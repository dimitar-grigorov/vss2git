using System.Linq;
using FluentAssertions;
using Hpdi.Vss2Git.IntegrationTests.Helpers;

namespace Hpdi.Vss2Git.IntegrationTests.Tests;

/// <summary>
/// Integration tests for Scenario01_Basic.
/// </summary>
public class BasicMigrationTests : IDisposable
{
    private readonly MigrationTestRunner _runner = new();

    public BasicMigrationTests()
    {
        _runner.Run("01_Basic");
    }

    [Fact]
    public void Migration_FilesAndContent()
    {
        var inspector = _runner.Inspector!;

        inspector.FileExists("TestProject/readme.txt").Should().BeTrue();
        inspector.FileExists("TestProject/main.c").Should().BeTrue();
        inspector.FileExists("TestProject/config.ini").Should().BeTrue();

        inspector.GetFileContent("TestProject/readme.txt")
            .Should().Contain("Version 3 - final");
        inspector.GetFileContent("TestProject/main.c")
            .Should().Contain("#include \"helper.h\"");
        inspector.GetFileContent("TestProject/config.ini")
            .Should().Contain("[settings]");

        // Old version should not be at HEAD
        inspector.GetFileContent("TestProject/readme.txt")
            .Should().NotContain("Version 1.", "readme was updated past v1");
    }

    [Fact]
    public void Migration_DeletedFileAndEmptyDirectory()
    {
        var inspector = _runner.Inspector!;

        inspector.FileExists("TestProject/SubFolder/helper.h").Should().BeFalse(
            "helper.h was deleted");

        // Only file in SubFolder was deleted → empty dir not tracked by git
        inspector.DirectoryExists("TestProject/SubFolder").Should().BeFalse(
            "empty directory after deletion");
    }

    [Fact]
    public void Migration_TagAndEmails()
    {
        var inspector = _runner.Inspector!;

        inspector.GetTags().Should().Contain(t =>
            t.Contains("v1") || t.Contains("1_0") || t.Contains("1.0"));

        inspector.GetCommits().Should().AllSatisfy(c =>
            c.Email.Should().EndWith("@test.local"));
    }

    [Fact]
    public void Migration_CommitQuality()
    {
        var commits = _runner.Inspector!.GetCommits();

        // Each 1.1s-separated operation → separate commit
        commits.Should().HaveCountGreaterThanOrEqualTo(7);

        // Commented operations preserve their messages; commentless ops have empty subject
        var withMessage = commits.Count(c => !string.IsNullOrWhiteSpace(c.Subject));
        withMessage.Should().BeGreaterThanOrEqualTo(5);
    }

    [Fact]
    public void Migration_ExactFileList()
    {
        var files = _runner.Inspector!.GetFileList();

        files.Should().HaveCount(3, "readme.txt + main.c + config.ini");
        files.Should().NotContain(f => f.Contains("helper.h"));
    }

    public void Dispose() => _runner.Dispose();
}
