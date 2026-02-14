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
using System.Threading;

namespace Hpdi.Vss2Git.Cli
{
    /// <summary>
    /// Console-based status reporter with single-line updates
    /// </summary>
    public class ConsoleStatusReporter : IStatusReporter
    {
        private readonly WorkQueue workQueue;
        private Timer pollTimer;
        private int lastLineLength = 0;
        private readonly object consoleLock = new object();

        /// <summary>
        /// Shared lock for all console output. Pass this to ConsoleUserInteraction
        /// to prevent timer callbacks from corrupting interactive prompts.
        /// </summary>
        public object ConsoleLock => consoleLock;

        /// <summary>
        /// Optional orchestrator reference for detailed statistics.
        /// Set this after construction to enable file/revision/changeset counts in status line.
        /// </summary>
        public MigrationOrchestrator Orchestrator { get; set; }

        public ConsoleStatusReporter(WorkQueue workQueue)
        {
            this.workQueue = workQueue;
        }

        public void Start()
        {
            // Poll every 500ms and update single line
            pollTimer = new Timer(UpdateStatus, null, 0, 500);
        }

        public void Stop()
        {
            pollTimer?.Dispose();
            pollTimer = null;

            lock (consoleLock)
            {
                if (lastLineLength > 0)
                {
                    Console.Write('\r' + new string(' ', lastLineLength) + '\r');
                    lastLineLength = 0;
                    Console.WriteLine();
                }
            }
        }

        private void UpdateStatus(object state)
        {
            try
            {
                lock (consoleLock)
                {
                    var status = workQueue.LastStatus ?? "Idle";
                    var elapsed = workQueue.ActiveTime;

                    // Build status line with optional detailed statistics
                    string line;
                    if (Orchestrator != null)
                    {
                        var files = Orchestrator.RevisionAnalyzer?.FileCount ?? 0;
                        var revisions = Orchestrator.RevisionAnalyzer?.RevisionCount ?? 0;
                        var changesets = Orchestrator.ChangesetBuilder?.Changesets.Count ?? 0;
                        line = $"{status} | Elapsed: {elapsed:hh\\:mm\\:ss} | Files: {files} | Revisions: {revisions} | Changesets: {changesets}";
                    }
                    else
                    {
                        line = $"{status} | Elapsed: {elapsed:hh\\:mm\\:ss}";
                    }

                    // Truncate if too long for console
                    int width;
                    try { width = Console.WindowWidth; } catch { width = 120; }
                    if (line.Length > width - 1)
                    {
                        line = line.Substring(0, width - 4) + "...";
                    }

                    // Overwrite in place: pad with spaces to cover any leftover chars from previous line
                    var padding = lastLineLength > line.Length
                        ? new string(' ', lastLineLength - line.Length)
                        : string.Empty;
                    Console.Write('\r' + line + padding);
                    lastLineLength = line.Length;
                }
            }
            catch (Exception)
            {
                // Ignore errors during status update (e.g., console closed)
            }
        }
    }
}
