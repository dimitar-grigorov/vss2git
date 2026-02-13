using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using FluentAssertions;
using Hpdi.Vss2Git.IntegrationTests.Helpers;

namespace Hpdi.Vss2Git.IntegrationTests.Tests;

/// <summary>
/// Integration tests for date-range (chunked) migration using Scenario06_DateRangeMigration.
/// The scenario has 3 phases separated by 2-second gaps:
///   Phase1: Create project, add file1 + file2, label Phase1_Release
///   Phase2: Edit file1, add file3, label Phase2_Release
///   Phase3: Edit file2, delete file3, add file4, label Phase3_Release
/// </summary>
public class DateRangeMigrationTests : IDisposable
{
    private const string ScenarioName = "06_DateRangeMigration";

    private readonly string _tempDir;
    private readonly MigrationTestRunner _fullRunner;
    private readonly List<GitCommitInfo> _fullCommits;
    private readonly List<string> _fullTags;

    // Date boundaries between phases (midpoints of the 2-second gaps)
    private readonly DateTime _splitAfterPhase1;
    private readonly DateTime _splitAfterPhase2;

    public DateRangeMigrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "vss2git_daterange_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);

        // Run full migration as baseline
        _fullRunner = new MigrationTestRunner();
        _fullRunner.Run(ScenarioName, configureAction: ConfigureForDateRange);

        _fullCommits = _fullRunner.Inspector!.GetCommits();
        _fullTags = _fullRunner.Inspector!.GetTags();

        // Find the two largest time gaps between consecutive commits.
        // These correspond to the 2-second Thread.Sleep between phases.
        var dates = _fullCommits
            .Select(c => DateTime.Parse(c.Date, CultureInfo.InvariantCulture))
            .OrderBy(d => d)
            .ToList();

        var gaps = new List<(TimeSpan gap, DateTime before, DateTime after)>();
        for (int i = 1; i < dates.Count; i++)
        {
            var gap = dates[i] - dates[i - 1];
            if (gap.TotalSeconds >= 1.5) // 2-second phase gaps are clearly > 1.5s
            {
                gaps.Add((gap, dates[i - 1], dates[i]));
            }
        }

        // Should have exactly 2 gaps (Phase1→Phase2 and Phase2→Phase3)
        gaps = gaps.OrderBy(g => g.before).ToList();

        if (gaps.Count < 2)
        {
            throw new InvalidOperationException(
                $"Expected at least 2 time gaps >= 1.5s in commit history, found {gaps.Count}. " +
                $"Commits: {string.Join(", ", _fullCommits.Select(c => $"[{c.Date}] {c.Subject}"))}");
        }

        // Midpoints of the gaps — guaranteed to be between phases
        _splitAfterPhase1 = gaps[0].before + TimeSpan.FromTicks(gaps[0].gap.Ticks / 2);
        _splitAfterPhase2 = gaps[1].before + TimeSpan.FromTicks(gaps[1].gap.Ticks / 2);
    }

    [Fact]
    public void FullMigration_ProducesAllContent()
    {
        var inspector = _fullRunner.Inspector!;

        // Phase 1 files
        inspector.FileExists("ChunkTest/file1.txt").Should().BeTrue("file1 created in Phase1");
        inspector.FileExists("ChunkTest/file2.txt").Should().BeTrue("file2 created in Phase1");

        // Phase 2 edit applied to file1
        inspector.GetFileContent("ChunkTest/file1.txt")
            .Should().Contain("Phase 2 updated content");

        // Phase 3: file3 deleted, file4 added, file2 updated
        inspector.FileExists("ChunkTest/file3.txt").Should().BeFalse("file3 was deleted in Phase3");
        inspector.FileExists("ChunkTest/file4.txt").Should().BeTrue("file4 added in Phase3");
        inspector.GetFileContent("ChunkTest/file2.txt")
            .Should().Contain("Phase 3 updated content");

        // All 3 labels should become tags
        _fullTags.Should().Contain(t => t.Contains("Phase1_Release"));
        _fullTags.Should().Contain(t => t.Contains("Phase2_Release"));
        _fullTags.Should().Contain(t => t.Contains("Phase3_Release"));
    }

    [Fact]
    public void FirstChunk_ContainsOnlyPhase1()
    {
        var gitDir = Path.Combine(_tempDir, "chunk1_only");
        Directory.CreateDirectory(gitDir);

        using var runner = new MigrationTestRunner();
        runner.RunInto(ScenarioName, gitDir, configureAction: config =>
        {
            ConfigureForDateRange(config);
            config.ToDate = _splitAfterPhase1;
        });

        var inspector = runner.Inspector!;

        // Phase 1 files with original content
        inspector.FileExists("ChunkTest/file1.txt").Should().BeTrue();
        inspector.GetFileContent("ChunkTest/file1.txt")
            .Should().Contain("Phase 1 content");
        inspector.FileExists("ChunkTest/file2.txt").Should().BeTrue();
        inspector.GetFileContent("ChunkTest/file2.txt")
            .Should().Contain("Phase 1 content");

        // Phase 2/3 files should not exist
        inspector.FileExists("ChunkTest/file3.txt").Should().BeFalse("file3 is added in Phase2");
        inspector.FileExists("ChunkTest/file4.txt").Should().BeFalse("file4 is added in Phase3");

        // Only Phase1 tag
        var tags = inspector.GetTags();
        tags.Should().Contain(t => t.Contains("Phase1_Release"));
        tags.Should().NotContain(t => t.Contains("Phase2_Release"));
        tags.Should().NotContain(t => t.Contains("Phase3_Release"));
    }

    [Fact]
    public void TwoChunks_ContainsPhase1And2()
    {
        var gitDir = Path.Combine(_tempDir, "two_chunks");
        Directory.CreateDirectory(gitDir);

        // Chunk 1: up to Phase1
        using (var r1 = new MigrationTestRunner())
        {
            r1.RunInto(ScenarioName, gitDir, configureAction: config =>
            {
                ConfigureForDateRange(config);
                config.ToDate = _splitAfterPhase1;
            });
        }

        // Chunk 2: Phase2 only (into same dir)
        using var r2 = new MigrationTestRunner();
        r2.RunInto(ScenarioName, gitDir, configureAction: config =>
        {
            ConfigureForDateRange(config);
            config.FromDate = _splitAfterPhase1;
            config.ToDate = _splitAfterPhase2;
        });

        var inspector = r2.Inspector!;

        // file1 should have Phase2 updated content
        inspector.GetFileContent("ChunkTest/file1.txt")
            .Should().Contain("Phase 2 updated content");

        // file2 still has Phase1 content (not edited until Phase3)
        inspector.GetFileContent("ChunkTest/file2.txt")
            .Should().Contain("Phase 1 content");

        // file3 should exist (added in Phase2, deleted in Phase3 which hasn't run yet)
        inspector.FileExists("ChunkTest/file3.txt").Should().BeTrue("file3 added in Phase2, not yet deleted");
        inspector.GetFileContent("ChunkTest/file3.txt")
            .Should().Contain("Phase 2 content");

        // file4 should not exist yet (Phase3)
        inspector.FileExists("ChunkTest/file4.txt").Should().BeFalse("file4 is added in Phase3");

        // Phase1 + Phase2 tags
        var tags = inspector.GetTags();
        tags.Should().Contain(t => t.Contains("Phase1_Release"));
        tags.Should().Contain(t => t.Contains("Phase2_Release"));
        tags.Should().NotContain(t => t.Contains("Phase3_Release"));
    }

    [Fact]
    public void AllThreeChunks_EqualsFullMigration()
    {
        var gitDir = Path.Combine(_tempDir, "three_chunks");
        Directory.CreateDirectory(gitDir);

        // Chunk 1: up to Phase1
        using (var r1 = new MigrationTestRunner())
        {
            r1.RunInto(ScenarioName, gitDir, configureAction: config =>
            {
                ConfigureForDateRange(config);
                config.ToDate = _splitAfterPhase1;
            });
        }

        // Chunk 2: Phase2
        using (var r2 = new MigrationTestRunner())
        {
            r2.RunInto(ScenarioName, gitDir, configureAction: config =>
            {
                ConfigureForDateRange(config);
                config.FromDate = _splitAfterPhase1;
                config.ToDate = _splitAfterPhase2;
            });
        }

        // Chunk 3: Phase3 onwards
        using var r3 = new MigrationTestRunner();
        r3.RunInto(ScenarioName, gitDir, configureAction: config =>
        {
            ConfigureForDateRange(config);
            config.FromDate = _splitAfterPhase2;
        });

        var inspector = r3.Inspector!;
        var chunkedCommits = inspector.GetCommits();
        var chunkedTags = inspector.GetTags();
        var chunkedFiles = inspector.GetFileList();

        // Same files as full migration
        var fullFiles = _fullRunner.Inspector!.GetFileList();
        chunkedFiles.Should().BeEquivalentTo(fullFiles, "chunked migration should produce same files as full");

        // Same number of commits
        chunkedCommits.Should().HaveCount(_fullCommits.Count,
            "chunked migration should produce same number of commits as full");

        // Same tags
        chunkedTags.Should().BeEquivalentTo(_fullTags, "chunked migration should produce same tags as full");

        // Same file content
        foreach (var file in fullFiles)
        {
            var fullContent = _fullRunner.Inspector!.GetFileContent(file);
            var chunkedContent = inspector.GetFileContent(file);
            chunkedContent.Should().Be(fullContent, $"content of {file} should match full migration");
        }
    }

    [Fact]
    public void CommitMessages_PreservedAcrossChunks()
    {
        var gitDir = Path.Combine(_tempDir, "commit_messages");
        Directory.CreateDirectory(gitDir);

        // Chunk 1: up to Phase1
        using (var r1 = new MigrationTestRunner())
        {
            r1.RunInto(ScenarioName, gitDir, configureAction: config =>
            {
                ConfigureForDateRange(config);
                config.ToDate = _splitAfterPhase1;
            });
        }

        // Chunk 2: Phase2
        using (var r2 = new MigrationTestRunner())
        {
            r2.RunInto(ScenarioName, gitDir, configureAction: config =>
            {
                ConfigureForDateRange(config);
                config.FromDate = _splitAfterPhase1;
                config.ToDate = _splitAfterPhase2;
            });
        }

        // Chunk 3: Phase3 onwards
        using var r3 = new MigrationTestRunner();
        r3.RunInto(ScenarioName, gitDir, configureAction: config =>
        {
            ConfigureForDateRange(config);
            config.FromDate = _splitAfterPhase2;
        });

        var chunkedSubjects = r3.Inspector!.GetCommits()
            .Select(c => c.Subject).ToList();
        var fullSubjects = _fullCommits
            .Select(c => c.Subject).ToList();

        chunkedSubjects.Should().BeEquivalentTo(fullSubjects,
            "commit messages should be identical between chunked and full migration");
    }

    private static void ConfigureForDateRange(MigrationConfiguration config)
    {
        config.AnyCommentSeconds = 1;
        config.SameCommentSeconds = 1;
    }

    public void Dispose()
    {
        _fullRunner.Dispose();
        try
        {
            if (Directory.Exists(_tempDir))
            {
                foreach (var file in Directory.EnumerateFiles(_tempDir, "*", SearchOption.AllDirectories))
                    File.SetAttributes(file, FileAttributes.Normal);
                Directory.Delete(_tempDir, true);
            }
        }
        catch
        {
            // Best-effort cleanup
        }
    }
}
