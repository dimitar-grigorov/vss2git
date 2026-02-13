using FluentAssertions;
using Hpdi.Vss2Git.IntegrationTests.Helpers;

namespace Hpdi.Vss2Git.IntegrationTests.Tests;

/// <summary>
/// Integration tests for Scenario01_Basic: add files, edit, delete, label.
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
    }

    [Fact]
    public void Migration_DeletedFileRemoved()
    {
        _runner.Inspector!.FileExists("TestProject/SubFolder/helper.h").Should().BeFalse(
            "helper.h was deleted and should not be in the final state");
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

    public void Dispose() => _runner.Dispose();
}
