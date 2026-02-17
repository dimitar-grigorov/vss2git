using FluentAssertions;
using Hpdi.VssLogicalLib;
using Xunit;

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
    }
}
