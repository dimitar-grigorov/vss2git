using FluentAssertions;
using Hpdi.VssLogicalLib;
using Xunit;

namespace Hpdi.Vss2Git.Tests
{
    /// <summary>
    /// Tests for ChangesetBuilder conflict detection.
    /// C3 bug: shared files have the same physical name across projects,
    /// causing false conflict splits when the same file is acted on
    /// from two different projects within one changeset window.
    /// </summary>
    public class ChangesetBuilderTests
    {
        private readonly WorkQueue _workQueue;
        private readonly RevisionAnalyzer _analyzer;

        public ChangesetBuilderTests()
        {
            _workQueue = new WorkQueue(1);
            // RevisionAnalyzer needs VssDatabase but ChangesetBuilder only
            // accesses SortedRevisions, so null is safe here.
            _analyzer = new RevisionAnalyzer(_workQueue, Logger.Null, null,
                new TestUserInteraction());
        }

        #region Helpers

        private static VssItemName MakeItem(string logical, string physical, bool isProject = false)
        {
            return new VssItemName(logical, physical, isProject);
        }

        private static Revision MakeRevision(DateTime dt, string user,
            VssItemName item, VssAction action, string comment = null)
        {
            return new Revision(dt, user, item, 1, comment, action);
        }

        private LinkedList<Changeset> BuildAndGetChangesets()
        {
            var builder = new ChangesetBuilder(_workQueue, Logger.Null,
                _analyzer, new TestUserInteraction());
            builder.AnyCommentThreshold = TimeSpan.FromSeconds(30);
            builder.SameCommentThreshold = TimeSpan.FromSeconds(60);
            builder.BuildChangesets();
            _workQueue.WaitIdle();
            return builder.Changesets;
        }

        private void AddRevision(DateTime dt, string user, VssItemName item,
            VssAction action, string comment = null)
        {
            var rev = MakeRevision(dt, user, item, action, comment);
            if (!_analyzer.SortedRevisions.TryGetValue(dt, out var list))
            {
                list = new List<Revision>();
                _analyzer.SortedRevisions[dt] = list;
            }
            list.Add(rev);
        }

        #endregion

        #region C3: Shared file false conflict

        [Fact]
        public void SharedFile_DeleteFromTwoProjects_ShouldNotSplitChangeset()
        {
            // Arrange: same shared file (AAAAAAAA) deleted from two projects at the same time
            var sharedFile = MakeItem("shared.txt", "AAAAAAAA");
            var projectA = MakeItem("$/ProjectA", "PAAAAAAA", isProject: true);
            var projectB = MakeItem("$/ProjectB", "PBBBBBB", isProject: true);

            var dt = new DateTime(2024, 1, 1, 10, 0, 0);

            // Project-level Delete actions: revision.Item = project, namedAction.Name = file
            AddRevision(dt, "user1", projectA, new VssDeleteAction(sharedFile));
            AddRevision(dt, "user1", projectB, new VssDeleteAction(sharedFile));

            // Act
            var changesets = BuildAndGetChangesets();

            // Assert: both deletes should be in ONE changeset (no false conflict)
            changesets.Should().HaveCount(1,
                "deleting the same shared file from two different projects is not a conflict");
            changesets.First.Value.Revisions.Should().HaveCount(2);
        }

        [Fact]
        public void SharedFile_AddToTwoProjects_ShouldNotSplitChangeset()
        {
            // Arrange: same file added to two projects (e.g., after a Share+Branch)
            var sharedFile = MakeItem("shared.txt", "AAAAAAAA");
            var projectA = MakeItem("$/ProjectA", "PAAAAAAA", isProject: true);
            var projectB = MakeItem("$/ProjectB", "PBBBBBB", isProject: true);

            var dt = new DateTime(2024, 1, 1, 10, 0, 0);

            AddRevision(dt, "user1", projectA, new VssAddAction(sharedFile));
            AddRevision(dt, "user1", projectB, new VssAddAction(sharedFile));

            // Act
            var changesets = BuildAndGetChangesets();

            // Assert: both adds should be in ONE changeset
            changesets.Should().HaveCount(1,
                "adding the same shared file to two different projects is not a conflict");
            changesets.First.Value.Revisions.Should().HaveCount(2);
        }

        [Fact]
        public void SameFile_TwoEditsInSameProject_ShouldStillSplitChangeset()
        {
            // Arrange: two edits to the SAME file in the SAME project — real conflict
            var file = MakeItem("file.txt", "AAAAAAAA");

            var dt1 = new DateTime(2024, 1, 1, 10, 0, 0);
            var dt2 = new DateTime(2024, 1, 1, 10, 0, 1);

            // File-level Edit actions: revision.Item = the file, namedAction = null
            AddRevision(dt1, "user1", file, new VssEditAction("AAAAAAAA"), "first edit");
            AddRevision(dt2, "user1", file, new VssEditAction("AAAAAAAA"), "second edit");

            // Act
            var changesets = BuildAndGetChangesets();

            // Assert: two edits to same file = real conflict → should split
            changesets.Should().HaveCount(2,
                "two edits to the same file are a real conflict");
        }

        [Fact]
        public void DifferentFiles_InSameProject_ShouldNotSplitChangeset()
        {
            // Arrange: edits to different files in same project — no conflict
            var fileA = MakeItem("fileA.txt", "AAAAAAAA");
            var fileB = MakeItem("fileB.txt", "BBBBBBBB");

            var dt1 = new DateTime(2024, 1, 1, 10, 0, 0);
            var dt2 = new DateTime(2024, 1, 1, 10, 0, 1);

            AddRevision(dt1, "user1", fileA, new VssEditAction("AAAAAAAA"), "edit A");
            AddRevision(dt2, "user1", fileB, new VssEditAction("BBBBBBBB"), "edit B");

            // Act
            var changesets = BuildAndGetChangesets();

            // Assert: different files → no conflict → one changeset
            changesets.Should().HaveCount(1);
            changesets.First.Value.Revisions.Should().HaveCount(2);
        }

        #endregion

        /// <summary>
        /// Minimal IUserInteraction for test use.
        /// </summary>
        private class TestUserInteraction : IUserInteraction
        {
            public ErrorAction ReportError(string message, ErrorActionOptions options)
                => ErrorAction.Abort;
            public bool Confirm(string message, string title)
                => true;
            public void ShowFatalError(string message, Exception exception) { }
        }
    }
}
