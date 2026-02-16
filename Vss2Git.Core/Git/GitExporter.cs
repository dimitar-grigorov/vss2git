/* Copyright 2009 HPDI, LLC
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *     http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Hpdi.VssLogicalLib;

namespace Hpdi.Vss2Git
{
    /// <summary>
    /// Replays and commits changesets into a new Git repository.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public class GitExporter : Worker
    {
        private readonly VssDatabase database;
        private readonly RevisionAnalyzer revisionAnalyzer;
        private readonly ChangesetBuilder changesetBuilder;
        private readonly MigrationConfiguration config;
        private readonly StreamCopier streamCopier = new StreamCopier();
        private readonly HashSet<string> tagsUsed = new HashSet<string>();
        private readonly PerformanceTracker perfTracker;
        private readonly List<string> pendingChangedPaths = new List<string>();
        private const int GcCommitInterval = 1000;
        private bool skipGitOperations;

        public GitExporter(WorkQueue workQueue, Logger logger,
            RevisionAnalyzer revisionAnalyzer, ChangesetBuilder changesetBuilder,
            MigrationConfiguration config,
            IUserInteraction userInteraction)
            : base(workQueue, logger, userInteraction)
        {
            this.database = revisionAnalyzer.Database;
            this.revisionAnalyzer = revisionAnalyzer;
            this.changesetBuilder = changesetBuilder;
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.perfTracker = config.EnablePerformanceTracking ? new PerformanceTracker() : null;
        }

        public void ExportToGit(string repoPath)
        {
            workQueue.AddLast(delegate(object work)
            {
                var stopwatch = Stopwatch.StartNew();

                logger.WriteSectionSeparator();
                LogStatus(work, "Initializing Git repository");

                // create repository directory if it does not exist
                if (!Directory.Exists(repoPath))
                {
                    Directory.CreateDirectory(repoPath);
                }

                // IMPORTANT: Use 'using' statement to ensure Dispose() is called
                using (IGitRepository git = CreateGitRepository(repoPath))
                {
                    // Determine encoding: use UTF-8 if transcoding, otherwise use VssEncoding as-is
                    var encoding = config.TranscodeComments ? Encoding.UTF8 : config.VssEncoding;
                    git.CommitEncoding = encoding;

                    if (!RetryCancel(delegate { git.Init(); }))
                    {
                        return;
                    }

                    if (encoding.WebName != "utf-8")
                    {
                        AbortRetryIgnore(delegate
                        {
                            git.SetConfig("i18n.commitencoding", encoding.WebName);
                        });
                    }

                    var pathMapper = new VssPathMapper();

                    // create mappings for root projects
                    foreach (var rootProject in revisionAnalyzer.RootProjects)
                    {
                        var rootPath = VssPathMapper.GetWorkingPath(repoPath, rootProject.Path, config.ExportProjectToGitRoot);
                        pathMapper.SetProjectPath(rootProject.PhysicalName, rootPath, rootProject.Path);
                    }

                    // replay each changeset
                    var changesetId = 1;
                    var changesets = changesetBuilder.Changesets;
                    var totalChangesets = changesets.Count;
                    var commitCount = 0;
                    var tagCount = 0;
                    var replayStopwatch = new Stopwatch();
                    var progressStopwatch = Stopwatch.StartNew();
                    var labels = new LinkedList<Revision>();
                    tagsUsed.Clear();
                    skipGitOperations = config.FromDate.HasValue;
                    foreach (var changeset in changesets)
                    {
                        // Date range filtering
                        if (skipGitOperations && changeset.DateTime >= config.FromDate.Value)
                        {
                            skipGitOperations = false;
                            logger.WriteLine("Date range start reached at {0}, beginning git operations",
                                changeset.DateTime);
                        }
                        if (config.ToDate.HasValue && changeset.DateTime > config.ToDate.Value)
                        {
                            logger.WriteLine("Date range end reached at {0}, stopping export",
                                changeset.DateTime);
                            break;
                        }

                        var changesetDesc = string.Format(CultureInfo.InvariantCulture,
                            "changeset {0} from {1}", changesetId, changeset.DateTime);

                        // progress logging every 100 changesets
                        if (changesetId % 100 == 0)
                        {
                            var elapsed = progressStopwatch.Elapsed;
                            var rate = changesetId / elapsed.TotalSeconds;
                            var remaining = (totalChangesets - changesetId) / rate;
                            logger.WriteSectionSeparator();
                            logger.WriteLine("PROGRESS: {0}/{1} changesets ({2:F1}%) - {3:F1} changesets/sec - ETA: {4:hh\\:mm\\:ss}",
                                changesetId, totalChangesets,
                                (double)changesetId / totalChangesets * 100.0,
                                rate,
                                TimeSpan.FromSeconds(remaining));
                            logger.WriteSectionSeparator();
                        }

                        // replay each revision in changeset
                        LogStatus(work, (skipGitOperations ? "Building state for " : "Replaying ") + changesetDesc);
                        labels.Clear();
                        replayStopwatch.Start();
                        bool needCommit;
                        try
                        {
                            needCommit = ReplayChangeset(pathMapper, changeset, git, labels);
                        }
                        finally
                        {
                            replayStopwatch.Stop();
                        }

                        if (workQueue.IsAborting)
                        {
                            return;
                        }

                        // Wait if suspended
                        if (!workQueue.WaitIfSuspended())
                        {
                            return; // Aborting
                        }

                        // commit changes (skip if outside date range)
                        if (needCommit && !skipGitOperations)
                        {
                            LogStatus(work, "Committing " + changesetDesc);
                            if (CommitChangeset(git, changeset))
                            {
                                ++commitCount;

                                // Periodically compact the git object database to
                                // prevent loose object accumulation from degrading
                                // performance (especially with LibGit2Sharp backend).
                                if (commitCount % GcCommitInterval == 0)
                                {
                                    LogStatus(work, "Compacting git repository");
                                    AbortRetryIgnore(delegate { git.Compact(); });
                                }
                            }
                        }
                        else
                        {
                            pendingChangedPaths.Clear();
                        }

                        if (workQueue.IsAborting)
                        {
                            return;
                        }

                        // Wait if suspended
                        if (!workQueue.WaitIfSuspended())
                        {
                            return; // Aborting
                        }

                        // create tags for any labels in the changeset (skip if outside date range)
                        if (labels.Count > 0 && !skipGitOperations)
                        {
                            foreach (Revision label in labels)
                            {
                                var labelName = ((VssLabelAction)label.Action).Label;
                                if (string.IsNullOrEmpty(labelName))
                                {
                                    logger.WriteLine("NOTE: Ignoring empty label");
                                }
                                else if (commitCount == 0)
                                {
                                    logger.WriteLine("NOTE: Ignoring label '{0}' before initial commit", labelName);
                                }
                                else
                                {
                                    var tagName = GetTagFromLabel(labelName);

                                    var tagMessage = "Creating tag " + tagName;
                                    if (tagName != labelName)
                                    {
                                        tagMessage += " for label '" + labelName + "'";
                                    }
                                    LogStatus(work, tagMessage);

                                    // annotated tags require (and are implied by) a tag message;
                                    // tools like Mercurial's git converter only import annotated tags
                                    var tagComment = label.Comment;
                                    if (string.IsNullOrEmpty(tagComment) && config.ForceAnnotatedTags)
                                    {
                                        // use the original VSS label as the tag message if none was provided
                                        tagComment = labelName;
                                    }

                                    if (AbortRetryIgnore(
                                        delegate
                                        {
                                            git.Tag(tagName, label.User, GetEmail(label.User),
                                                tagComment, label.DateTime);
                                        }))
                                    {
                                        ++tagCount;
                                    }
                                }
                            }
                        }

                        ++changesetId;
                    }

                    // Clean up empty directories
                    LogStatus(work, "Cleaning up empty directories");
                    var emptyDirsRemoved = RemoveEmptyDirectories(repoPath);

                    stopwatch.Stop();

                    logger.WriteSectionSeparator();
                    logger.WriteLine("Git export complete in {0:HH:mm:ss}", new DateTime(stopwatch.ElapsedTicks));
                    logger.WriteLine("Replay time: {0:HH:mm:ss}", new DateTime(replayStopwatch.ElapsedTicks));
                    logger.WriteLine("Git time: {0:HH:mm:ss}", new DateTime(git.ElapsedTime.Ticks));
                    logger.WriteLine("Git commits: {0}", commitCount);
                    logger.WriteLine("Git tags: {0}", tagCount);
                    logger.WriteLine("Empty directories removed: {0}", emptyDirsRemoved);

                    perfTracker?.WriteSummary(logger, stopwatch.Elapsed);
                } // Dispose git repository (CRITICAL: finalizes fast-import)
            });
        }

        private bool ReplayChangeset(VssPathMapper pathMapper, Changeset changeset,
            IGitRepository git, LinkedList<Revision> labels)
        {
            var needCommit = false;

            // Order by time, then by action priority for same-timestamp tiebreaking
            var revisions = changeset.Revisions
                .OrderBy(r => r.DateTime)
                .ThenBy(r => GetActionPriority(r.Action.Type));

            foreach (Revision revision in revisions)
            {
                if (workQueue.IsAborting)
                {
                    break;
                }

                AbortRetryIgnore(delegate
                {
                    needCommit |= ReplayRevision(pathMapper, revision, git, labels);
                });
            }
            return needCommit;
        }

        /// <summary>
        /// Defines causal ordering for action types within a changeset.
        /// Lower values are processed first.
        /// </summary>
        private static int GetActionPriority(VssActionType actionType) => actionType switch
        {
            VssActionType.Create  => 0,  // ignored but logically first
            VssActionType.Label   => 1,  // deferred to after commit
            VssActionType.Add     => 2,  // structural: adds items
            VssActionType.Share   => 2,
            VssActionType.Recover => 2,
            VssActionType.Restore => 2,
            VssActionType.MoveFrom => 3, // must come before MoveTo
            VssActionType.Branch  => 4,  // must come after Share
            VssActionType.Pin     => 5,
            VssActionType.Edit    => 6,
            VssActionType.Rename  => 7,
            VssActionType.Archive => 8,  // currently ignored
            VssActionType.MoveTo  => 9,  // cleanup after MoveFrom
            VssActionType.Delete  => 10,
            VssActionType.Destroy => 11,
            _                     => 6,
        };

        private bool ReplayRevision(VssPathMapper pathMapper, Revision revision,
            IGitRepository git, LinkedList<Revision> labels)
        {
            var needCommit = false;
            var actionType = revision.Action.Type;
            if (revision.Item.IsProject)
            {
                // note that project path (and therefore target path) can be
                // null if a project was moved and its original location was
                // subsequently destroyed
                var project = revision.Item;
                var projectName = project.LogicalName;
                var projectPath = pathMapper.GetProjectPath(project.PhysicalName);
                var projectDesc = projectPath;
                if (projectPath == null)
                {
                    projectDesc = revision.Item.ToString();
                    logger.WriteLine("NOTE: {0} is currently unmapped", project);
                }

                VssItemName target = null;
                string targetPath = null;
                var namedAction = revision.Action as VssNamedAction;
                if (namedAction != null)
                {
                    target = namedAction.Name;
                    if (projectPath != null)
                    {
                        targetPath = Path.Combine(projectPath, target.LogicalName);
                    }
                }

                bool isAddAction = false;
                bool writeProject = false;
                bool writeFile = false;
                VssItemInfo itemInfo = null;
                switch (actionType)
                {
                    case VssActionType.Label:
                        // defer tagging until after commit
                        labels.AddLast(revision);
                        break;

                    case VssActionType.Create:
                        // ignored; items are actually created when added to a project
                        break;

                    case VssActionType.Add:
                    case VssActionType.Share:
                        logger.WriteLine("{0}: {1} {2}", projectDesc, actionType, target.LogicalName);
                        itemInfo = pathMapper.AddItem(project, target);
                        isAddAction = true;
                        break;

                    case VssActionType.Recover:
                        logger.WriteLine("{0}: {1} {2}", projectDesc, actionType, target.LogicalName);
                        itemInfo = pathMapper.RecoverItem(project, target);
                        isAddAction = true;
                        break;

                    case VssActionType.Delete:
                    case VssActionType.Destroy:
                        {
                            logger.WriteLine("{0}: {1} {2}", projectDesc, actionType, target.LogicalName);
                            itemInfo = pathMapper.DeleteItem(project, target);
                            if (!skipGitOperations && targetPath != null && !itemInfo.Destroyed)
                            {
                                if (target.IsProject)
                                {
                                    if (Directory.Exists(targetPath))
                                    {
                                        if (((VssProjectInfo)itemInfo).ContainsFiles())
                                        {
                                            git.Remove(targetPath, true);
                                            needCommit = true;
                                        }
                                        else
                                        {
                                            // git doesn't care about directories with no files
                                            Directory.Delete(targetPath, true);
                                        }
                                    }
                                }
                                else
                                {
                                    if (File.Exists(targetPath))
                                    {
                                        // not sure how it can happen, but a project can evidently
                                        // contain another file with the same logical name, so check
                                        // that this is not the case before deleting the file
                                        if (pathMapper.ProjectContainsLogicalName(project, target))
                                        {
                                            logger.WriteLine("NOTE: {0} contains another file named {1}; not deleting file",
                                                projectDesc, target.LogicalName);
                                        }
                                        else
                                        {
                                            File.Delete(targetPath);
                                            pendingChangedPaths.Add(targetPath);
                                            needCommit = true;
                                        }
                                    }
                                }
                            }
                        }
                        break;

                    case VssActionType.Rename:
                        {
                            var renameAction = (VssRenameAction)revision.Action;
                            logger.WriteLine("{0}: {1} {2} to {3}",
                                projectDesc, actionType, renameAction.OriginalName, target.LogicalName);
                            itemInfo = pathMapper.RenameItem(target);
                            if (!skipGitOperations && targetPath != null && !itemInfo.Destroyed)
                            {
                                var sourcePath = Path.Combine(projectPath, renameAction.OriginalName);
                                if (target.IsProject ? Directory.Exists(sourcePath) : File.Exists(sourcePath))
                                {
                                    // renaming a file or a project that contains files?
                                    var projectInfo = itemInfo as VssProjectInfo;
                                    if (projectInfo == null || projectInfo.ContainsFiles())
                                    {
                                        CaseSensitiveRename(sourcePath, targetPath, git.Move);
                                        if (target.IsProject)
                                            TranslatePendingPaths(sourcePath, targetPath);
                                        needCommit = true;
                                    }
                                    else
                                    {
                                        // git doesn't care about directories with no files
                                        CaseSensitiveRename(sourcePath, targetPath, Directory.Move);
                                    }
                                }
                                else
                                {
                                    logger.WriteLine("NOTE: Skipping rename because {0} does not exist", sourcePath);
                                }
                            }
                        }
                        break;

                    case VssActionType.MoveFrom:
                        // if both MoveFrom & MoveTo are present (e.g.
                        // one of them has not been destroyed), only one
                        // can succeed, so check that the source exists
                        {
                            var moveFromAction = (VssMoveFromAction)revision.Action;
                            logger.WriteLine("{0}: Move from {1} to {2}",
                                projectDesc, moveFromAction.OriginalProject, targetPath ?? target.LogicalName);
                            var sourcePath = pathMapper.GetProjectPath(target.PhysicalName);
                            var projectInfo = pathMapper.MoveProjectFrom(
                                project, target, moveFromAction.OriginalProject);
                            if (!skipGitOperations && targetPath != null && !projectInfo.Destroyed)
                            {
                                // Clean up destination if it already exists to prevent stale files.
                                // This can happen when pathMapper resolves a different name than
                                // what's on disk, causing the source check below to fail and
                                // writeProject to re-create files without removing the old ones.
                                if (Directory.Exists(targetPath) &&
                                    !string.Equals(sourcePath, targetPath, StringComparison.OrdinalIgnoreCase))
                                {
                                    logger.WriteLine("NOTE: MoveFrom destination already exists, removing: {0}", targetPath);
                                    git.Remove(targetPath, true);
                                    needCommit = true;
                                }

                                if (sourcePath != null && Directory.Exists(sourcePath))
                                {
                                    if (projectInfo.ContainsFiles())
                                    {
                                        git.Move(sourcePath, targetPath);
                                        TranslatePendingPaths(sourcePath, targetPath);
                                        needCommit = true;
                                    }
                                    else
                                    {
                                        // git doesn't care about directories with no files
                                        Directory.Move(sourcePath, targetPath);
                                    }
                                }
                                else
                                {
                                    // project was moved from a now-destroyed project
                                    writeProject = true;
                                }
                            }
                        }
                        break;

                    case VssActionType.MoveTo:
                        {
                            // handle actual moves in MoveFrom; this just does cleanup of destroyed projects
                            var moveToAction = (VssMoveToAction)revision.Action;
                            logger.WriteLine("{0}: Move to {1} from {2}",
                                projectDesc, moveToAction.NewProject, targetPath ?? target.LogicalName);
                            var projectInfo = pathMapper.MoveProjectTo(
                                project, target, moveToAction.NewProject);
                            if (!skipGitOperations && projectInfo.Destroyed && targetPath != null && Directory.Exists(targetPath))
                            {
                                // project was moved to a now-destroyed project; remove empty directory
                                Directory.Delete(targetPath, true);
                            }
                        }
                        break;

                    case VssActionType.Pin:
                        {
                            var pinAction = (VssPinAction)revision.Action;
                            if (pinAction.Pinned)
                            {
                                logger.WriteLine("{0}: Pin {1}", projectDesc, target.LogicalName);
                                itemInfo = pathMapper.PinItem(project, target);
                            }
                            else
                            {
                                logger.WriteLine("{0}: Unpin {1}", projectDesc, target.LogicalName);
                                itemInfo = pathMapper.UnpinItem(project, target);
                                writeFile = !skipGitOperations && !itemInfo.Destroyed;
                            }
                        }
                        break;

                    case VssActionType.Branch:
                        {
                            var branchAction = (VssBranchAction)revision.Action;
                            logger.WriteLine("{0}: {1} {2}", projectDesc, actionType, target.LogicalName);
                            itemInfo = pathMapper.BranchFile(project, target, branchAction.Source);
                            // branching within the project might happen after branching of the file
                            writeFile = !skipGitOperations;
                        }
                        break;

                    case VssActionType.Archive:
                        // currently ignored
                        {
                            var archiveAction = (VssArchiveAction)revision.Action;
                            logger.WriteLine("{0}: Archive {1} to {2} (ignored)",
                                projectDesc, target.LogicalName, archiveAction.ArchivePath);
                        }
                        break;

                    case VssActionType.Restore:
                        {
                            var restoreAction = (VssRestoreAction)revision.Action;
                            logger.WriteLine("{0}: Restore {1} from archive {2}",
                                projectDesc, target.LogicalName, restoreAction.ArchivePath);
                            itemInfo = pathMapper.AddItem(project, target);
                            isAddAction = true;
                        }
                        break;
                }

                if (targetPath != null)
                {
                    if (isAddAction)
                    {
                        if (revisionAnalyzer.IsDestroyed(target.PhysicalName) &&
                            !database.ItemExists(target.PhysicalName))
                        {
                            logger.WriteLine("NOTE: Skipping destroyed file: {0}", targetPath);
                            itemInfo.Destroyed = true;
                        }
                        else if (!skipGitOperations)
                        {
                            if (target.IsProject)
                            {
                                Directory.CreateDirectory(targetPath);
                                writeProject = true;
                            }
                            else
                            {
                                writeFile = true;
                            }
                        }
                    }

                    if (writeProject && pathMapper.IsProjectRooted(target.PhysicalName))
                    {
                        // create all contained subdirectories
                        foreach (var projectInfo in pathMapper.GetAllProjects(target.PhysicalName))
                        {
                            logger.WriteLine("{0}: Creating subdirectory {1}",
                                projectDesc, projectInfo.LogicalName);
                                    Directory.CreateDirectory(projectInfo.GetPath());
                        }

                        // write current rev of all contained files
                        foreach (var fileInfo in pathMapper.GetAllFiles(target.PhysicalName))
                        {
                            if (WriteRevision(pathMapper, actionType, fileInfo.PhysicalName,
                                fileInfo.Version, target.PhysicalName))
                            {
                                // one or more files were written
                                needCommit = true;
                            }
                        }
                    }
                    else if (writeFile)
                    {
                        // write current rev to working path
                        int version = pathMapper.GetFileVersion(target.PhysicalName);
                        if (WriteRevisionTo(target.PhysicalName, version, targetPath))
                        {
                            // git add -A in CommitChangeset will stage this file
                            needCommit = true;
                        }
                    }
                }
            }
            // item is a file, not a project
            else if (actionType == VssActionType.Edit || actionType == VssActionType.Branch)
            {
                // if the action is Branch, the following code is necessary only if the item
                // was branched from a file that is not part of the migration subset; it will
                // make sure we start with the correct revision instead of the first revision

                var target = revision.Item;

                // update current rev
                pathMapper.SetFileVersion(target, revision.Version);

                // write current rev to all sharing projects
                if (!skipGitOperations)
                {
                    if (WriteRevision(pathMapper, actionType, target.PhysicalName,
                        revision.Version, null))
                    {
                        needCommit = true;
                    }
                }
            }
            return needCommit;
        }

        /// <summary>
        /// After a directory move/rename, translates any pending changed paths
        /// from the old directory to the new one so AddAll processes them correctly.
        /// </summary>
        private void TranslatePendingPaths(string oldDir, string newDir)
        {
            TranslatePendingPaths(pendingChangedPaths, oldDir, newDir);
        }

        internal static void TranslatePendingPaths(List<string> paths, string oldDir, string newDir)
        {
            var oldPrefix = oldDir + Path.DirectorySeparatorChar;
            for (int i = 0; i < paths.Count; i++)
            {
                if (paths[i].StartsWith(oldPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    paths[i] = newDir + paths[i].Substring(oldDir.Length);
                }
            }
        }

        private bool CommitChangeset(IGitRepository git, Changeset changeset)
        {
            var result = false;
            var paths = pendingChangedPaths.ToArray();
            pendingChangedPaths.Clear();
            AbortRetryIgnore(delegate
            {
                result = git.AddAll(paths) &&
                    git.Commit(changeset.User, GetEmail(changeset.User),
                    changeset.Comment ?? config.DefaultComment, changeset.DateTime);
            });
            return result;
        }

        private bool RetryCancel(ThreadStart work)
        {
            return AbortRetryIgnore(work, ErrorActionOptions.RetryCancel);
        }

        private bool AbortRetryIgnore(ThreadStart work)
        {
            return AbortRetryIgnore(work, ErrorActionOptions.AbortRetryIgnore);
        }

        private bool AbortRetryIgnore(ThreadStart work, ErrorActionOptions options)
        {
            bool retry;
            do
            {
                try
                {
                    work();
                    return true;
                }
                catch (Exception e)
                {
                    var message = LogException(e);

                    message += "\nSee log file for more information.";

                    if (config.IgnoreErrors)
                    {
                        retry = false;
                        continue;
                    }

                    var action = userInteraction.ReportError(message, options);
                    switch (action)
                    {
                        case ErrorAction.Retry:
                            retry = true;
                            break;
                        case ErrorAction.Ignore:
                            retry = false;
                            break;
                        default:
                            retry = false;
                            workQueue.Abort();
                            break;
                    }
                }
            } while (retry);
            return false;
        }

        private string GetEmail(string user)
        {
            // TODO: user-defined mapping of user names to email addresses
            return user.ToLower().Replace(' ', '.') + "@" + config.DefaultEmailDomain;
        }

        private string GetTagFromLabel(string label)
        {
            // git tag names must be valid filenames, so replace sequences of
            // invalid characters with an underscore
            var baseTag = Regex.Replace(label, "[^A-Za-z0-9_-]+", "_");

            // git tags are global, whereas VSS tags are local, so ensure
            // global uniqueness by appending a number; since the file system
            // may be case-insensitive, ignore case when hashing tags
            var tag = baseTag;
            for (int i = 2; !tagsUsed.Add(tag.ToUpperInvariant()); ++i)
            {
                tag = baseTag + "-" + i;
            }

            return tag;
        }

        private bool WriteRevision(VssPathMapper pathMapper, VssActionType actionType,
            string physicalName, int version, string underProject)
        {
            var needCommit = false;
            var paths = pathMapper.GetFilePaths(physicalName, underProject);
            foreach (string path in paths)
            {
                logger.WriteLine("{0}: {1} revision {2}", path, actionType, version);
                if (WriteRevisionTo(physicalName, version, path))
                {
                    // git add -A in CommitChangeset will stage this file
                    needCommit = true;
                }
            }
            return needCommit;
        }

        private bool WriteRevisionTo(string physical, int version, string destPath)
        {
            VssFile item;
            VssFileRevision revision;
            Stream contents;
            try
            {
                using (perfTracker?.Start("VSS:getItem"))
                    item = (VssFile)database.GetItemPhysical(physical);
                using (perfTracker?.Start("VSS:getRevision"))
                    revision = item.GetRevision(version);
                using (perfTracker?.Start("VSS:getContents"))
                    contents = revision.GetContents();
            }
            catch (Exception e)
            {
                // log an error for missing data files or versions, but keep processing
                var message = ExceptionFormatter.Format(e);
                logger.WriteLine("ERROR: {0}", message);
                logger.WriteLine(e);
                return false;
            }

            // propagate exceptions here (e.g. disk full) to abort/retry/ignore
            using (perfTracker?.Start("VSS:writeFile"))
            {
                using (contents)
                {
                    WriteStream(contents, destPath);
                }
            }

            // try to use the first revision (for this branch) as the create time,
            // since the item creation time doesn't seem to be meaningful
            using (perfTracker?.Start("VSS:setTimestamps"))
            {
                var createDateTime = item.Created;
                using (var revEnum = item.Revisions.GetEnumerator())
                {
                    if (revEnum.MoveNext())
                    {
                        createDateTime = revEnum.Current.DateTime;
                    }
                }

                // set file creation and update timestamps
                File.SetCreationTimeUtc(destPath, TimeZoneInfo.ConvertTimeToUtc(createDateTime));
                File.SetLastWriteTimeUtc(destPath, TimeZoneInfo.ConvertTimeToUtc(revision.DateTime));
            }

            pendingChangedPaths.Add(destPath);
            return true;
        }

        private void WriteStream(Stream inputStream, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            using (var outputStream = new FileStream(
                path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                streamCopier.Copy(inputStream, outputStream);
            }
        }

        private delegate void RenameDelegate(string sourcePath, string destPath);

        private void CaseSensitiveRename(string sourcePath, string destPath, RenameDelegate renamer)
        {
            if (sourcePath.Equals(destPath, StringComparison.OrdinalIgnoreCase))
            {
                // workaround for case-only renames on case-insensitive file systems:

                var sourceDir = Path.GetDirectoryName(sourcePath);
                var sourceFile = Path.GetFileName(sourcePath);
                var destDir = Path.GetDirectoryName(destPath);
                var destFile = Path.GetFileName(destPath);

                if (sourceDir != destDir)
                {
                    // recursively rename containing directories that differ in case
                    CaseSensitiveRename(sourceDir, destDir, renamer);

                    // fix up source path based on renamed directory
                    sourcePath = Path.Combine(destDir, sourceFile);
                }

                if (sourceFile != destFile)
                {
                    // use temporary filename to rename files that differ in case
                    var tempPath = sourcePath + ".mvtmp";
                    CaseSensitiveRename(sourcePath, tempPath, renamer);
                    CaseSensitiveRename(tempPath, destPath, renamer);
                }
            }
            else
            {
                renamer(sourcePath, destPath);
            }
        }

        private IGitRepository CreateGitRepository(string repoPath)
        {
            switch (config.GitBackend)
            {
                case GitBackend.LibGit2Sharp:
                    logger.WriteLine("Using LibGit2Sharp backend");
                    return new LibGit2SharpRepository(repoPath, logger, perfTracker);
                case GitBackend.FastImport:
                    logger.WriteLine("Using git fast-import backend");
                    return new FastImportRepository(repoPath, logger, perfTracker);
                default:
                    logger.WriteLine("Using git.exe process backend");
                    return new GitWrapper(repoPath, logger, perfTracker);
            }
        }

        private int RemoveEmptyDirectories(string rootPath)
        {
            int removedCount = 0;
            var gitDir = Path.Combine(rootPath, ".git");
            var gitDirPrefix = gitDir + Path.DirectorySeparatorChar;

            // Process from deepest to shallowest directories
            var allDirs = Directory.GetDirectories(rootPath, "*", SearchOption.AllDirectories)
                .Where(d => d != gitDir && !d.StartsWith(gitDirPrefix))
                .OrderByDescending(d => d.Length);

            foreach (var dir in allDirs)
            {
                try
                {
                    if (!Directory.EnumerateFileSystemEntries(dir).Any())
                    {
                        logger.WriteLine("Removing empty directory: {0}",
                            dir.Substring(rootPath.Length).TrimStart(Path.DirectorySeparatorChar));
                        Directory.Delete(dir, false);
                        removedCount++;
                    }
                }
                catch (Exception ex)
                {
                    logger.WriteLine("WARNING: Could not remove directory {0}: {1}", dir, ex.Message);
                }
            }

            return removedCount;
        }
    }
}
