using FluentAssertions;
using Xunit;

namespace Hpdi.Vss2Git.Tests
{
    /// <summary>
    /// Tests for GitExporter.TranslatePendingPaths which updates pending file
    /// paths after a directory move/rename so that AddAll processes deletions
    /// at the correct (new) location.
    ///
    /// Bug fixed: After a directory move, files deleted earlier in the changeset
    /// had stale paths pointing to the old directory. LibGit2Sharp's AddAll would
    /// try to remove entries at the old paths (no-op since they were moved), leaving
    /// "phantom" files in the git tree that didn't exist on disk.
    /// </summary>
    public class TranslatePendingPathsTests
    {
        [Fact]
        public void TranslatePendingPaths_FilesInsideMovedDirectory_PathsUpdated()
        {
            // Arrange: files were written/deleted inside "C:\repo\OldDir"
            var paths = new List<string>
            {
                @"C:\repo\OldDir\file1.txt",
                @"C:\repo\OldDir\sub\file2.txt",
                @"C:\repo\OtherDir\unrelated.txt"
            };

            // Act: directory moved from OldDir to NewDir
            GitExporter.TranslatePendingPaths(paths, @"C:\repo\OldDir", @"C:\repo\NewDir");

            // Assert
            paths.Should().Equal(
                @"C:\repo\NewDir\file1.txt",
                @"C:\repo\NewDir\sub\file2.txt",
                @"C:\repo\OtherDir\unrelated.txt"
            );
        }

        [Fact]
        public void TranslatePendingPaths_NoMatchingPaths_NothingChanged()
        {
            var paths = new List<string>
            {
                @"C:\repo\ProjectA\file.txt",
                @"C:\repo\ProjectB\other.txt"
            };

            GitExporter.TranslatePendingPaths(paths, @"C:\repo\ProjectC", @"C:\repo\ProjectD");

            paths.Should().Equal(
                @"C:\repo\ProjectA\file.txt",
                @"C:\repo\ProjectB\other.txt"
            );
        }

        [Fact]
        public void TranslatePendingPaths_EmptyList_NoError()
        {
            var paths = new List<string>();

            GitExporter.TranslatePendingPaths(paths, @"C:\old", @"C:\new");

            paths.Should().BeEmpty();
        }

        [Fact]
        public void TranslatePendingPaths_CaseInsensitiveMatching_PathsUpdated()
        {
            // Windows paths are case-insensitive
            var paths = new List<string>
            {
                @"C:\Repo\OLDDIR\file.txt",
                @"C:\Repo\olddir\sub\file.txt"
            };

            GitExporter.TranslatePendingPaths(paths, @"C:\Repo\OldDir", @"C:\Repo\NewDir");

            paths.Should().Equal(
                @"C:\Repo\NewDir\file.txt",
                @"C:\Repo\NewDir\sub\file.txt"
            );
        }

        [Fact]
        public void TranslatePendingPaths_DirectoryNameIsPrefix_OnlyExactDirectoryMatches()
        {
            // "OldDir" should NOT match "OldDirectory" - the separator check prevents this
            var paths = new List<string>
            {
                @"C:\repo\OldDir\file.txt",
                @"C:\repo\OldDirectory\other.txt"
            };

            GitExporter.TranslatePendingPaths(paths, @"C:\repo\OldDir", @"C:\repo\NewDir");

            paths.Should().Equal(
                @"C:\repo\NewDir\file.txt",
                @"C:\repo\OldDirectory\other.txt"  // NOT changed - different directory
            );
        }

        [Fact]
        public void TranslatePendingPaths_NestedMove_PathsUpdated()
        {
            // Directory moved to a deeper location
            var paths = new List<string>
            {
                @"C:\repo\Source\data.txt"
            };

            GitExporter.TranslatePendingPaths(paths, @"C:\repo\Source", @"C:\repo\Target\Sub\Source");

            paths.Should().Equal(@"C:\repo\Target\Sub\Source\data.txt");
        }

        [Fact]
        public void TranslatePendingPaths_CaseOnlyRename_PathsUpdated()
        {
            // Case-only directory rename: RxLIB → RxLib
            var paths = new List<string>
            {
                @"C:\repo\RxLIB\component.pas",
                @"C:\repo\RxLIB\sub\unit.pas"
            };

            GitExporter.TranslatePendingPaths(paths, @"C:\repo\RxLIB", @"C:\repo\RxLib");

            paths.Should().Equal(
                @"C:\repo\RxLib\component.pas",
                @"C:\repo\RxLib\sub\unit.pas"
            );
        }
    }
}
