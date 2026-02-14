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
using System.IO;
using System.Linq;
using System.Reflection;
using Hpdi.VssLogicalLib;

namespace Hpdi.Vss2Git
{
    /// <summary>
    /// Orchestrates the complete VSS to Git migration pipeline
    /// </summary>
    public class MigrationOrchestrator
    {
        private readonly MigrationConfiguration config;
        private readonly WorkQueue workQueue;
        private readonly IUserInteraction userInteraction;
        private readonly IStatusReporter statusReporter;
        private Logger logger;

        public RevisionAnalyzer RevisionAnalyzer { get; private set; }
        public ChangesetBuilder ChangesetBuilder { get; private set; }

        public MigrationOrchestrator(
            MigrationConfiguration config,
            WorkQueue workQueue,
            IUserInteraction userInteraction,
            IStatusReporter statusReporter)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.workQueue = workQueue ?? throw new ArgumentNullException(nameof(workQueue));
            this.userInteraction = userInteraction ?? throw new ArgumentNullException(nameof(userInteraction));
            this.statusReporter = statusReporter ?? throw new ArgumentNullException(nameof(statusReporter));
        }

        /// <summary>
        /// Run the complete migration pipeline
        /// </summary>
        /// <returns>True if migration started successfully</returns>
        public bool Run()
        {
            // Validate configuration
            var validation = config.Validate();
            if (!validation.IsValid)
            {
                var errors = string.Join("\n", validation.Errors);
                userInteraction.ShowFatalError($"Invalid configuration:\n{errors}", null);
                return false;
            }

            // Open logger
            logger = string.IsNullOrEmpty(config.LogFile)
                ? Logger.Null
                : new Logger(config.LogFile);

            var queueOwnsLogger = false;
            try
            {
                logger.WriteLine("VSS2Git version {0}", Assembly.GetExecutingAssembly().GetName().Version);
                logger.WriteLine("VSS encoding: {0} (CP: {1}, IANA: {2})",
                    config.VssEncoding.EncodingName,
                    config.VssEncoding.CodePage,
                    config.VssEncoding.WebName);
                logger.WriteLine("Comment transcoding: {0}",
                    config.TranscodeComments ? "enabled" : "disabled");
                logger.WriteLine("Ignore errors: {0}",
                    config.IgnoreErrors ? "enabled" : "disabled");

                // Open VSS database
                var df = new VssDatabaseFactory(config.VssDirectory);
                df.Encoding = config.VssEncoding;
                var db = df.Open();

                var path = config.VssProject.Trim();

                // Default to root project if path is empty
                if (string.IsNullOrEmpty(path))
                {
                    path = "$";
                    logger.WriteLine("VSS project path was empty, defaulting to root: $");
                }

                logger.WriteLine("VSS project: {0}", path);
                VssItem item;
                try
                {
                    item = db.GetItem(path);
                }
                catch (VssPathException ex)
                {
                    userInteraction.ShowFatalError($"Invalid project path: {ex.Message}", ex);
                    return false;
                }

                var project = item as VssProject;
                if (project == null)
                {
                    userInteraction.ShowFatalError($"{path} is not a project", null);
                    return false;
                }

                // Analyze revisions
                RevisionAnalyzer = new RevisionAnalyzer(workQueue, logger, db, userInteraction);
                if (!string.IsNullOrEmpty(config.VssExcludePaths))
                {
                    RevisionAnalyzer.ExcludeFiles = config.VssExcludePaths;
                }
                RevisionAnalyzer.AddItem(project);

                // Build changesets
                ChangesetBuilder = new ChangesetBuilder(workQueue, logger, RevisionAnalyzer, userInteraction);
                ChangesetBuilder.AnyCommentThreshold = TimeSpan.FromSeconds(config.AnyCommentSeconds);
                ChangesetBuilder.SameCommentThreshold = TimeSpan.FromSeconds(config.SameCommentSeconds);
                ChangesetBuilder.BuildChangesets();

                // Export to Git
                if (!string.IsNullOrEmpty(config.GitDirectory))
                {
                    var outDir = config.GitDirectory.Trim();

                    // Check if output directory is not empty (skip when --force or --from-date)
                    if (!config.Force && !config.FromDate.HasValue &&
                        Directory.Exists(outDir) && Directory.EnumerateFileSystemEntries(outDir).Any())
                    {
                        if (!userInteraction.Confirm(
                            "The output directory is not empty. Do you want to continue?",
                            "Output directory not empty"))
                        {
                            return false;
                        }
                    }

                    var gitExporter = new GitExporter(workQueue, logger,
                        RevisionAnalyzer, ChangesetBuilder, config, userInteraction);
                    gitExporter.ExportToGit(outDir);
                }

                // Wait for completion — queue takes ownership of logger disposal
                workQueue.Idle += delegate
                {
                    logger.Dispose();
                    logger = Logger.Null;
                    statusReporter.Stop();
                };
                queueOwnsLogger = true;

                statusReporter.Start();

                return true;
            }
            catch (Exception ex)
            {
                userInteraction.ShowFatalError("Unhandled exception during migration", ex);
                return false;
            }
            finally
            {
                if (!queueOwnsLogger)
                {
                    logger?.Dispose();
                    logger = Logger.Null;
                }
            }
        }

        /// <summary>
        /// Abort running migration
        /// </summary>
        public void Abort()
        {
            workQueue.Abort();
        }

        /// <summary>
        /// Pause running migration (if supported)
        /// </summary>
        public void Pause()
        {
            workQueue.Suspend();
        }

        /// <summary>
        /// Resume paused migration
        /// </summary>
        public void Resume()
        {
            workQueue.Resume();
        }
    }
}
