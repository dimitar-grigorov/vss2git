using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Hpdi.VssLogicalLib;

namespace Hpdi.Vss2Git.Tests
{
    public class VssPathMapperTests
    {
        private static VssItemName MakeProject(string logical, string physical)
            => new VssItemName(logical, physical, isProject: true);

        private static VssItemName MakeFile(string logical, string physical)
            => new VssItemName(logical, physical, isProject: false);

        [Fact]
        public void BranchFile_PreservesBranchPointVersion_WhenSetByFileLevelBranch()
        {
            var mapper = new VssPathMapper();
            var project = MakeProject("Project", "PROJAAAA");
            var oldFile = MakeFile("File.txt", "OLDAAAAA");
            var newFile = MakeFile("File.txt", "NEWAAAAA");

            mapper.AddItem(project, oldFile);

            // Simulate file-level Branch setting the branch point version
            mapper.SetFileVersion(newFile, 13);

            // Project-level Branch should NOT overwrite version 13
            mapper.BranchFile(project, newFile, oldFile);

            mapper.GetFileVersion(newFile.PhysicalName).Should().Be(13);
        }

        [Fact]
        public void BranchFile_InheritsOldFileVersion_WhenNewFileHasDefault()
        {
            var mapper = new VssPathMapper();
            var project = MakeProject("Project", "PROJAAAA");
            var oldFile = MakeFile("File.txt", "OLDAAAAA");
            var newFile = MakeFile("File.txt", "NEWAAAAA");

            mapper.AddItem(project, oldFile);
            mapper.SetFileVersion(oldFile, 10);

            mapper.BranchFile(project, newFile, oldFile);

            mapper.GetFileVersion(newFile.PhysicalName).Should().Be(10);
        }

        #region Path-Map Tests

        private VssPathMapper CreatePathMapMapper(string repoPath, string scanRootVssPath, string scanRootPhysical,
            Dictionary<string, string> mappings)
        {
            var mapper = new VssPathMapper();
            // Register scan root as tracking-only (null path)
            mapper.SetProjectPath(scanRootPhysical, null, scanRootVssPath);
            mapper.SetPathMappings(repoPath, mappings);
            return mapper;
        }

        [Fact]
        public void PathMap_SubprojectMatchingMapping_IsPromotedToRoot()
        {
            var mapper = CreatePathMapMapper(@"C:\git", "$/Deploy/Speedy", "ROOTAAAA",
                new Dictionary<string, string>
                {
                    ["$/Deploy/Speedy/www.speedy.bg - Portal"] = "portal.speedy.bg"
                });

            var root = MakeProject("Speedy", "ROOTAAAA");
            var portal = MakeProject("www.speedy.bg - Portal", "PORTAAAA");

            mapper.AddItem(root, portal);

            var path = mapper.GetProjectPath("PORTAAAA");
            path.Should().Be(Path.Combine(@"C:\git", "portal.speedy.bg"));
        }

        [Fact]
        public void PathMap_UnmappedSubproject_ReturnsNullPath()
        {
            var mapper = CreatePathMapMapper(@"C:\git", "$/Deploy/Speedy", "ROOTAAAA",
                new Dictionary<string, string>
                {
                    ["$/Deploy/Speedy/www.speedy.bg - Portal"] = "portal.speedy.bg"
                });

            var root = MakeProject("Speedy", "ROOTAAAA");
            var other = MakeProject("OtherProject", "OTHAAAAA");

            mapper.AddItem(root, other);

            mapper.GetProjectPath("OTHAAAAA").Should().BeNull();
        }

        [Fact]
        public void PathMap_FilesUnderMappedProject_ResolveCorrectly()
        {
            var mapper = CreatePathMapMapper(@"C:\git", "$/Deploy/Speedy", "ROOTAAAA",
                new Dictionary<string, string>
                {
                    ["$/Deploy/Speedy/www.speedy.bg - Portal"] = "portal.speedy.bg"
                });

            var root = MakeProject("Speedy", "ROOTAAAA");
            var portal = MakeProject("www.speedy.bg - Portal", "PORTAAAA");
            var file = MakeFile("index.html", "FILEAAAA");

            mapper.AddItem(root, portal);
            mapper.AddItem(portal, file);

            var paths = mapper.GetFilePaths("FILEAAAA", null);
            paths.Should().ContainSingle()
                .Which.Should().Be(Path.Combine(@"C:\git", "portal.speedy.bg", "index.html"));
        }

        [Fact]
        public void PathMap_FilesUnderUnmappedProject_ReturnNoPaths()
        {
            var mapper = CreatePathMapMapper(@"C:\git", "$/Deploy/Speedy", "ROOTAAAA",
                new Dictionary<string, string>
                {
                    ["$/Deploy/Speedy/www.speedy.bg - Portal"] = "portal.speedy.bg"
                });

            var root = MakeProject("Speedy", "ROOTAAAA");
            var other = MakeProject("OtherProject", "OTHAAAAA");
            var file = MakeFile("readme.txt", "FILEBBBB");

            mapper.AddItem(root, other);
            mapper.AddItem(other, file);

            mapper.GetFilePaths("FILEBBBB", null).Should().BeEmpty();
        }

        [Fact]
        public void PathMap_NestedSubprojectUnderMappedRoot_ResolvesCorrectly()
        {
            var mapper = CreatePathMapMapper(@"C:\git", "$/Deploy/Speedy", "ROOTAAAA",
                new Dictionary<string, string>
                {
                    ["$/Deploy/Speedy/www.speedy.bg - Portal"] = "portal.speedy.bg"
                });

            var root = MakeProject("Speedy", "ROOTAAAA");
            var portal = MakeProject("www.speedy.bg - Portal", "PORTAAAA");
            var images = MakeProject("images", "IMGAAAAA");

            mapper.AddItem(root, portal);
            mapper.AddItem(portal, images);

            mapper.GetProjectPath("IMGAAAAA").Should().Be(
                Path.Combine(@"C:\git", "portal.speedy.bg", "images"));
        }

        [Fact]
        public void PathMap_MultipleMappings_EachPromotedIndependently()
        {
            var mapper = CreatePathMapMapper(@"C:\git", "$/Deploy/Speedy", "ROOTAAAA",
                new Dictionary<string, string>
                {
                    ["$/Deploy/Speedy/www.speedy.bg - Portal"] = "portal.speedy.bg",
                    ["$/Deploy/Speedy/www.speedy.bg - Inventory"] = "itinventory.speedy.bg"
                });

            var root = MakeProject("Speedy", "ROOTAAAA");
            var portal = MakeProject("www.speedy.bg - Portal", "PORTAAAA");
            var inventory = MakeProject("www.speedy.bg - Inventory", "INVAAAAA");

            mapper.AddItem(root, portal);
            mapper.AddItem(root, inventory);

            mapper.GetProjectPath("PORTAAAA").Should().Be(
                Path.Combine(@"C:\git", "portal.speedy.bg"));
            mapper.GetProjectPath("INVAAAAA").Should().Be(
                Path.Combine(@"C:\git", "itinventory.speedy.bg"));
        }

        [Fact]
        public void PathMap_DeeplyNestedMapping_IsPromoted()
        {
            // Map $/Deploy/Speedy/web - Romania/www.dpd.ro - Portal => portal.dpd.ro
            var mapper = CreatePathMapMapper(@"C:\git", "$/Deploy/Speedy", "ROOTAAAA",
                new Dictionary<string, string>
                {
                    ["$/Deploy/Speedy/web - Romania/www.dpd.ro - Portal"] = "portal.dpd.ro"
                });

            var root = MakeProject("Speedy", "ROOTAAAA");
            var romania = MakeProject("web - Romania", "ROMAAAAA");
            var dpd = MakeProject("www.dpd.ro - Portal", "DPDAAAAA");
            var file = MakeFile("default.aspx", "FILEAAAA");

            mapper.AddItem(root, romania);
            mapper.AddItem(romania, dpd);
            mapper.AddItem(dpd, file);

            mapper.GetProjectPath("DPDAAAAA").Should().Be(
                Path.Combine(@"C:\git", "portal.dpd.ro"));
            // romania should be unmapped
            mapper.GetProjectPath("ROMAAAAA").Should().BeNull();
            // file under mapped project resolves
            mapper.GetFilePaths("FILEAAAA", null).Should().ContainSingle()
                .Which.Should().Be(Path.Combine(@"C:\git", "portal.dpd.ro", "default.aspx"));
        }

        [Fact]
        public void PathMap_RenameToMatchMapping_PromotesOnRename()
        {
            var mapper = CreatePathMapMapper(@"C:\git", "$/Deploy/Speedy", "ROOTAAAA",
                new Dictionary<string, string>
                {
                    ["$/Deploy/Speedy/www.speedy.bg - Portal"] = "portal.speedy.bg"
                });

            var root = MakeProject("Speedy", "ROOTAAAA");
            var oldName = MakeProject("old-portal-name", "PORTAAAA");

            // Add with non-matching name
            mapper.AddItem(root, oldName);
            mapper.GetProjectPath("PORTAAAA").Should().BeNull();

            // Rename to matching name
            var newName = MakeProject("www.speedy.bg - Portal", "PORTAAAA");
            mapper.RenameItem(newName);

            mapper.GetProjectPath("PORTAAAA").Should().Be(
                Path.Combine(@"C:\git", "portal.speedy.bg"));
        }

        [Fact]
        public void PathMap_CaseInsensitiveMatching()
        {
            var mapper = CreatePathMapMapper(@"C:\git", "$/Deploy/Speedy", "ROOTAAAA",
                new Dictionary<string, string>
                {
                    ["$/deploy/speedy/WWW.SPEEDY.BG - PORTAL"] = "portal.speedy.bg"
                });

            var root = MakeProject("Speedy", "ROOTAAAA");
            var portal = MakeProject("www.speedy.bg - Portal", "PORTAAAA");

            mapper.AddItem(root, portal);

            mapper.GetProjectPath("PORTAAAA").Should().Be(
                Path.Combine(@"C:\git", "portal.speedy.bg"));
        }

        [Fact]
        public void PathMap_ScanRootReturnsNullPath()
        {
            var mapper = CreatePathMapMapper(@"C:\git", "$/Deploy/Speedy", "ROOTAAAA",
                new Dictionary<string, string>
                {
                    ["$/Deploy/Speedy/www.speedy.bg - Portal"] = "portal.speedy.bg"
                });

            // The scan root itself should return null path (tracking-only)
            mapper.GetProjectPath("ROOTAAAA").Should().BeNull();
        }

        [Fact]
        public void PathMap_RecoverItem_PromotesMatchingProject()
        {
            var mapper = CreatePathMapMapper(@"C:\git", "$/Deploy/Speedy", "ROOTAAAA",
                new Dictionary<string, string>
                {
                    ["$/Deploy/Speedy/www.speedy.bg - Portal"] = "portal.speedy.bg"
                });

            var root = MakeProject("Speedy", "ROOTAAAA");
            var portal = MakeProject("www.speedy.bg - Portal", "PORTAAAA");

            // Add then delete
            mapper.AddItem(root, portal);
            mapper.DeleteItem(root, portal);

            // Recover should re-promote
            mapper.RecoverItem(root, portal);

            mapper.GetProjectPath("PORTAAAA").Should().Be(
                Path.Combine(@"C:\git", "portal.speedy.bg"));
        }

        [Fact]
        public void PathMap_MoveProjectFrom_PromotesMatchingProject()
        {
            var mapper = CreatePathMapMapper(@"C:\git", "$/Deploy/Speedy", "ROOTAAAA",
                new Dictionary<string, string>
                {
                    ["$/Deploy/Speedy/www.speedy.bg - Portal"] = "portal.speedy.bg"
                });

            var root = MakeProject("Speedy", "ROOTAAAA");
            var portal = MakeProject("www.speedy.bg - Portal", "PORTAAAA");

            mapper.MoveProjectFrom(root, portal, "$/OldLocation/www.speedy.bg - Portal");

            mapper.GetProjectPath("PORTAAAA").Should().Be(
                Path.Combine(@"C:\git", "portal.speedy.bg"));
        }

        [Fact]
        public void PathMap_DestroyedThenRecreatedProject_SecondProjectPromoted()
        {
            // Reproduces: services(UJUC) added under dpd.ro, promoted, destroyed.
            // Then services(YDVC) added under dpd.ro with same name — should also promote.
            var mapper = CreatePathMapMapper(@"C:\git", "$/Root", "ROOTAAAA",
                new Dictionary<string, string>
                {
                    ["$/Root/parent/services"] = "services.out"
                });

            var root = MakeProject("Root", "ROOTAAAA");
            var parent = MakeProject("parent", "PARAAAAA");
            mapper.AddItem(root, parent);

            // First project: add, promote, then destroy
            var svc1 = MakeProject("services", "SVC1AAAA");
            mapper.AddItem(parent, svc1);
            mapper.GetProjectPath("SVC1AAAA").Should().Be(
                Path.Combine(@"C:\git", "services.out"), "first services should be promoted");

            mapper.DeleteItem(parent, svc1);

            // Second project: add with same name, different physical
            var svc2 = MakeProject("services", "SVC2AAAA");
            mapper.AddItem(parent, svc2);
            mapper.GetProjectPath("SVC2AAAA").Should().Be(
                Path.Combine(@"C:\git", "services.out"), "second services should also be promoted");

            // Files under second project should resolve
            var file = MakeFile("index.php", "FILEAAAA");
            mapper.AddItem(svc2, file);
            mapper.GetFilePaths("FILEAAAA", null).Should().ContainSingle()
                .Which.Should().Be(Path.Combine(@"C:\git", "services.out", "index.php"));
        }

        [Fact]
        public void PathMap_DeepNestedWithDestroyRecreate_PromotesCorrectly()
        {
            // Mirrors: $/Deploy/Speedy/web - Romania/dpd.ro/services
            var mapper = CreatePathMapMapper(@"C:\git", "$/Deploy/Speedy", "ROOTAAAA",
                new Dictionary<string, string>
                {
                    ["$/Deploy/Speedy/web - Romania/dpd.ro/services"] = "services.dpd.ro",
                    ["$/Deploy/Speedy/web - Romania/www.dpd.ro - Portal"] = "portal.dpd.ro",
                    ["$/Deploy/Speedy/www.dpd.ro - Portal"] = "portal.dpd.ro"
                });

            var root = MakeProject("Speedy", "ROOTAAAA");
            var webRo = MakeProject("web - Romania", "GASCAAAA");
            mapper.AddItem(root, webRo);

            // portal gets promoted and detached from web - Romania
            var portal = MakeProject("www.dpd.ro - Portal", "LTKCAAAA");
            mapper.AddItem(root, portal);
            mapper.GetProjectPath("LTKCAAAA").Should().NotBeNull("portal should be promoted from root");

            // dpd.ro added under web - Romania
            var dpdRo = MakeProject("dpd.ro", "TJUCAAAA");
            mapper.AddItem(webRo, dpdRo);

            // first services: add, then destroy
            var svc1 = MakeProject("services", "UJUCAAAA");
            mapper.AddItem(dpdRo, svc1);
            mapper.GetProjectPath("UJUCAAAA").Should().Be(
                Path.Combine(@"C:\git", "services.dpd.ro"), "first services should promote");
            mapper.DeleteItem(dpdRo, svc1);

            // second services: same name, different physical
            var svc2 = MakeProject("services", "YDVCAAAA");
            mapper.AddItem(dpdRo, svc2);

            var path = mapper.GetProjectPath("YDVCAAAA");
            path.Should().Be(Path.Combine(@"C:\git", "services.dpd.ro"),
                "second services should also promote");

            // file under second services
            var file = MakeFile("index.php", "FILEAAAA");
            mapper.AddItem(svc2, file);
            mapper.GetFilePaths("FILEAAAA", null).Should().ContainSingle()
                .Which.Should().Be(Path.Combine(@"C:\git", "services.dpd.ro", "index.php"));
        }

        #endregion
    }
}
