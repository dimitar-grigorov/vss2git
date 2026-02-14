using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Hpdi.Vss2Git.IntegrationTests.Helpers;

namespace Hpdi.Vss2Git.IntegrationTests.Tests;

/// <summary>
/// Validates that LibGit2Sharp and FastImport backends produce identical git
/// output to the Process backend for every integration test scenario.
/// Each test runs the scenario with Process (reference) and the target backend,
/// then compares file lists, file content, tags, and commit structure.
/// </summary>
public class CrossBackendValidationTests
{
    /// <summary>
    /// Scenario definitions: (scenarioName, vssProject, configureAction).
    /// Matches the existing integration test classes.
    /// </summary>
    private static readonly (string Name, string VssProject, Action<MigrationConfiguration>? Configure)[] Scenarios =
    {
        ("01_Basic", "$", null),
        ("02_RenamesAndMoves", "$", null),
        ("03_SharingAndBranching", "$", null),
        ("04_PinsAndLabels", "$", null),
        ("05_DeleteAndRecover", "$", null),
        ("06_DateRangeMigration", "$", config => { config.AnyCommentSeconds = 1; config.SameCommentSeconds = 1; }),
        ("07_SharedFileDeleteRecover", "$", null),
        ("08_ProjectMoveChain", "$", null),
        ("09_DeleteRecoverProject", "$", null),
        ("10_SharedFilePinDuringShare", "$", null),
        ("11_UnmappedProjectRevisions", "$/Target", null),
        ("12_TimestampCollision", "$", null),
    };

    public static IEnumerable<object[]> ScenarioBackendCombinations()
    {
        var backends = new[] { GitBackend.LibGit2Sharp, GitBackend.FastImport };
        foreach (var scenario in Scenarios)
        {
            foreach (var backend in backends)
            {
                yield return new object[] { scenario.Name, scenario.VssProject, backend };
            }
        }
    }

    [Theory]
    [MemberData(nameof(ScenarioBackendCombinations))]
    public void Migration_MatchesProcessBackend(string scenarioName, string vssProject, GitBackend backend)
    {
        // Find scenario config
        var scenario = Scenarios.First(s => s.Name == scenarioName);

        // Run with Process backend (reference)
        using var refRunner = new MigrationTestRunner();
        refRunner.Run(scenarioName, vssProject, configureAction: config =>
        {
            config.GitBackend = GitBackend.Process;
            scenario.Configure?.Invoke(config);
        });

        // Run with target backend
        using var testRunner = new MigrationTestRunner();
        testRunner.Run(scenarioName, vssProject, configureAction: config =>
        {
            config.GitBackend = backend;
            scenario.Configure?.Invoke(config);
        });

        var refInspector = refRunner.Inspector!;
        var testInspector = testRunner.Inspector!;

        // Compare file lists (case-insensitive — git backends may preserve
        // case differently for case-only renames on Windows)
        var refFiles = refInspector.GetFileList()
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
        var testFiles = testInspector.GetFileList()
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
        testFiles.Select(f => f.ToLowerInvariant()).Should().Equal(
            refFiles.Select(f => f.ToLowerInvariant()),
            $"{backend} should produce same file list as Process for {scenarioName}");

        // Compare file content — use each backend's own file paths since
        // git show is case-sensitive even on Windows
        for (int i = 0; i < refFiles.Count; i++)
        {
            var refContent = refInspector.GetFileContent(refFiles[i]);
            var testContent = testInspector.GetFileContent(testFiles[i]);
            testContent.Should().Be(refContent,
                $"{backend} content of '{refFiles[i]}' should match Process for {scenarioName}");
        }

        // Compare tags
        var refTags = refInspector.GetTags().OrderBy(t => t).ToList();
        var testTags = testInspector.GetTags().OrderBy(t => t).ToList();
        testTags.Should().BeEquivalentTo(refTags,
            $"{backend} should produce same tags as Process for {scenarioName}");

        // Compare commit count
        var refCommits = refInspector.GetCommits();
        var testCommits = testInspector.GetCommits();
        testCommits.Should().HaveCount(refCommits.Count,
            $"{backend} should produce same number of commits as Process for {scenarioName}");

        // Compare commit messages (in order)
        var refSubjects = refCommits.Select(c => c.Subject).ToList();
        var testSubjects = testCommits.Select(c => c.Subject).ToList();
        testSubjects.Should().Equal(refSubjects,
            $"{backend} commit messages should match Process for {scenarioName}");

        // Compare commit authors (in order)
        var refAuthors = refCommits.Select(c => c.Author).ToList();
        var testAuthors = testCommits.Select(c => c.Author).ToList();
        testAuthors.Should().Equal(refAuthors,
            $"{backend} commit authors should match Process for {scenarioName}");
    }
}
