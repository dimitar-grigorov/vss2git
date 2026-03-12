using FluentAssertions;
using Hpdi.VssLogicalLib;

namespace Hpdi.Vss2Git.Tests;

/// <summary>
/// Tests for SeedProjectTree restoring parent-child links stripped by ssarc -v.
/// </summary>
public class SeedProjectTreeTests
{
    private static VssItemName MakeProject(string logical, string physical)
        => new VssItemName(logical, physical, isProject: true);

    private static VssItemName MakeFile(string logical, string physical)
        => new VssItemName(logical, physical, isProject: false);

    [Fact]
    public void UnseededSubProject_HasNullPath()
    {
        // SubProject exists but its Add action was archived away
        var mapper = new VssPathMapper();
        mapper.SetProjectPath("ROOTAAAA", @"C:\repo", "$");

        var root = MakeProject("Root", "ROOTAAAA");
        var sub = MakeProject("SubProject", "SUBAAAAA");
        var file = MakeFile("file.txt", "FILEAAAA");

        mapper.AddItem(sub, file); // creates SubProject but with no parent link

        mapper.GetProjectPath("SUBAAAAA").Should().BeNull(
            "SubProject was never added to Root — its Add action was archived away");
    }

    [Fact]
    public void SeededSubProject_HasValidPath()
    {
        // Seed the parent-child link (simulating SeedProjectTree)
        var mapper = new VssPathMapper();
        mapper.SetProjectPath("ROOTAAAA", @"C:\repo", "$");

        var root = MakeProject("Root", "ROOTAAAA");
        var sub = MakeProject("SubProject", "SUBAAAAA");

        // Seed parent-child link
        mapper.AddItem(root, sub);

        mapper.GetProjectPath("SUBAAAAA").Should().Be(@"C:\repo\SubProject");
    }

    [Fact]
    public void SeededSubProject_FilesHaveValidPaths()
    {
        var mapper = new VssPathMapper();
        mapper.SetProjectPath("ROOTAAAA", @"C:\repo", "$");

        var root = MakeProject("Root", "ROOTAAAA");
        var sub = MakeProject("SubProject", "SUBAAAAA");
        var file = MakeFile("file.txt", "FILEAAAA");

        mapper.AddItem(root, sub);
        // Add file to sub-project
        mapper.AddItem(sub, file);

        var paths = mapper.GetFilePaths("FILEAAAA", null).ToList();
        paths.Should().ContainSingle()
            .Which.Should().Be(@"C:\repo\SubProject\file.txt");
    }

    [Fact]
    public void UnseededSubProject_FilesHaveNoPaths()
    {
        var mapper = new VssPathMapper();
        mapper.SetProjectPath("ROOTAAAA", @"C:\repo", "$");

        var sub = MakeProject("SubProject", "SUBAAAAA");
        var file = MakeFile("file.txt", "FILEAAAA");

        // SubProject was never linked to Root
        mapper.AddItem(sub, file);

        var paths = mapper.GetFilePaths("FILEAAAA", null).ToList();
        paths.Should().BeEmpty(
            "SubProject is not rooted — its Add action was archived away");
    }

    [Fact]
    public void DeepNestedProject_RequiresFullChainSeeding()
    {
        // Both Level1 and Level2 predate history
        var mapper = new VssPathMapper();
        mapper.SetProjectPath("ROOTAAAA", @"C:\repo", "$");

        var root = MakeProject("Root", "ROOTAAAA");
        var level1 = MakeProject("Level1", "LVL1AAAA");
        var level2 = MakeProject("Level2", "LVL2AAAA");
        var file = MakeFile("file.txt", "FILEAAAA");

        // Seed full chain
        mapper.AddItem(root, level1);
        mapper.AddItem(level1, level2);

        mapper.AddItem(level2, file);

        var paths = mapper.GetFilePaths("FILEAAAA", null).ToList();
        paths.Should().ContainSingle()
            .Which.Should().Be(@"C:\repo\Level1\Level2\file.txt");
    }

    [Fact]
    public void DeepNestedProject_PartialSeed_StillUnrooted()
    {
        // Level1→Root link is missing
        var mapper = new VssPathMapper();
        mapper.SetProjectPath("ROOTAAAA", @"C:\repo", "$");

        var level1 = MakeProject("Level1", "LVL1AAAA");
        var level2 = MakeProject("Level2", "LVL2AAAA");
        var file = MakeFile("file.txt", "FILEAAAA");

        mapper.AddItem(level1, level2);
        mapper.AddItem(level2, file);

        var paths = mapper.GetFilePaths("FILEAAAA", null).ToList();
        paths.Should().BeEmpty("Level1 is not linked to Root — chain is broken");
    }

    [Fact]
    public void Seeding_ThenHistoricalAdd_SameParent_IsIdempotent()
    {
        // Duplicate AddItem with same parent should be harmless
        var mapper = new VssPathMapper();
        mapper.SetProjectPath("ROOTAAAA", @"C:\repo", "$");

        var root = MakeProject("Root", "ROOTAAAA");
        var sub = MakeProject("SubProject", "SUBAAAAA");

        // Seed
        mapper.AddItem(root, sub);
        // Historical Add (same parent)
        mapper.AddItem(root, sub);

        mapper.GetProjectPath("SUBAAAAA").Should().Be(@"C:\repo\SubProject");
    }

    [Fact]
    public void Seeding_ThenHistoricalAdd_DifferentParent_Corrects()
    {
        // Historical Add should move SubProject from ParentB to ParentA
        var mapper = new VssPathMapper();
        mapper.SetProjectPath("ROOTAAAA", @"C:\repo", "$");

        var root = MakeProject("Root", "ROOTAAAA");
        var parentA = MakeProject("ParentA", "PRNAAAAA");
        var parentB = MakeProject("ParentB", "PRNBAAAA");
        var sub = MakeProject("SubProject", "SUBAAAAA");

        mapper.AddItem(root, parentA);
        mapper.AddItem(root, parentB);

        // Seed under ParentB
        mapper.AddItem(parentB, sub);
        mapper.GetProjectPath("SUBAAAAA").Should().Be(@"C:\repo\ParentB\SubProject");

        // Historical Add under ParentA
        mapper.AddItem(parentA, sub);
        mapper.GetProjectPath("SUBAAAAA").Should().Be(@"C:\repo\ParentA\SubProject",
            "historical Add should correct the seeded parent");
    }

    [Fact]
    public void Seeding_WithCurrentName_HistoricalAdd_OverwritesName()
    {
        // Historical Add should overwrite seeded name to the old name
        var mapper = new VssPathMapper();
        mapper.SetProjectPath("ROOTAAAA", @"C:\repo", "$");

        var root = MakeProject("Root", "ROOTAAAA");
        var subCurrent = MakeProject("NewName", "SUBAAAAA");
        var subHistorical = MakeProject("OldName", "SUBAAAAA");

        mapper.AddItem(root, subCurrent);
        mapper.GetProjectPath("SUBAAAAA").Should().Be(@"C:\repo\NewName");

        mapper.AddItem(root, subHistorical);
        mapper.GetProjectPath("SUBAAAAA").Should().Be(@"C:\repo\OldName",
            "AddItem should overwrite to the historical name");
    }
}
