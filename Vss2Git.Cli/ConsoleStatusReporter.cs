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
        private readonly MigrationOrchestrator orchestrator;
        private Timer pollTimer;
        private int lastLineLength = 0;
        private readonly object consoleLock = new object();

        public ConsoleStatusReporter(WorkQueue workQueue, MigrationOrchestrator orchestrator)
        {
            this.workQueue = workQueue;
            this.orchestrator = orchestrator;
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

            // Clear the status line and move to next line
            lock (consoleLock)
            {
                if (lastLineLength > 0)
                {
                    Console.Write('\r' + new string(' ', lastLineLength) + '\r');
                    lastLineLength = 0;
                }
                Console.WriteLine();
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
                    var files = orchestrator.RevisionAnalyzer?.FileCount ?? 0;
                    var revisions = orchestrator.RevisionAnalyzer?.RevisionCount ?? 0;
                    var changesets = orchestrator.ChangesetBuilder?.Changesets.Count ?? 0;

                    // Clear previous line (carriage return + spaces)
                    if (lastLineLength > 0)
                    {
                        Console.Write('\r' + new string(' ', lastLineLength) + '\r');
                    }

                    // Build status line
                    var line = $"{status} | Elapsed: {elapsed:hh\\:mm\\:ss} | Files: {files} | Revisions: {revisions} | Changesets: {changesets}";

                    // Truncate if too long for console
                    if (line.Length > Console.WindowWidth - 1)
                    {
                        line = line.Substring(0, Console.WindowWidth - 4) + "...";
                    }

                    Console.Write(line);
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
