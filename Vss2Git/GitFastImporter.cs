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
using System.IO;
using System.Linq;
using System.Text;

namespace Hpdi.Vss2Git
{
    /// <summary>
    /// Git repository implementation using git fast-import for high performance.
    /// Writes to a single long-running git fast-import process instead of spawning
    /// a new process for each git command.
    /// </summary>
    /// <author>Dan Cristoloveanu, based on GitWrapper by Trevor Robinson</author>
    class GitFastImporter : IGitRepository, IDisposable
    {
        private readonly string repoPath;
        private readonly Logger logger;
        private readonly Stopwatch stopwatch = new Stopwatch();
        private Process fastImportProcess;
        private Stream fastImportStream;
        private int markCounter = 1;
        private int commitCount = 0;
        private Encoding commitEncoding = Encoding.UTF8;

        // Performance tracking
        private readonly PerformanceTracker perfTracker;

        // Buffered operations (staged for next commit)
        private readonly List<PendingFileOp> pendingOps = new List<PendingFileOp>();

        // Config settings to apply after import
        private Dictionary<string, string> postImportConfigs;

        // Error tracking
        private readonly StringBuilder stderrBuffer = new StringBuilder();

        public TimeSpan ElapsedTime
        {
            get { return stopwatch.Elapsed; }
        }

        public Encoding CommitEncoding
        {
            get { return commitEncoding; }
            set { commitEncoding = value; }
        }

        public GitFastImporter(string repoPath, Logger logger)
        {
            this.repoPath = repoPath;
            this.logger = logger;
            this.perfTracker = new PerformanceTracker(logger);
        }

        public void Init()
        {
            logger.WriteLine("Initializing git repository with fast-import...");

            // Create repo directory
            Directory.CreateDirectory(repoPath);

            // Initialize git repository
            var initProcess = Process.Start(new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "init",
                WorkingDirectory = repoPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });

            if (initProcess == null)
            {
                throw new Exception("Failed to start git init process");
            }

            initProcess.WaitForExit();
            if (initProcess.ExitCode != 0)
            {
                var error = initProcess.StandardError.ReadToEnd();
                throw new Exception($"git init failed: {error}");
            }

            logger.WriteLine("Git repository initialized");

            // Start fast-import process
            fastImportProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "fast-import --quiet --done",
                    WorkingDirectory = repoPath,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            fastImportProcess.Start();

            // Use the BaseStream directly - no StreamWriter wrapper
            // This avoids all buffering and encoding issues
            fastImportStream = fastImportProcess.StandardInput.BaseStream;

            // Start async error reader
            StartErrorReader();

            stopwatch.Start();
            logger.WriteLine("Git fast-import process started");
        }

        public void SetConfig(string name, string value)
        {
            // Store for later application (after fast-import completes)
            if (postImportConfigs == null)
            {
                postImportConfigs = new Dictionary<string, string>();
            }

            postImportConfigs[name] = value;
            logger.WriteLine($"Config queued: {name} = {value}");
        }

        public bool Add(string path)
        {
            if (!File.Exists(path))
            {
                logger.WriteLine($"WARNING: File not found for add: {path}");
                return false;
            }

            perfTracker.StartFileRead();
            var content = File.ReadAllBytes(path);
            perfTracker.EndFileRead();

            var relativePath = GetRelativePath(repoPath, path);
            pendingOps.Add(new PendingFileOp
            {
                Type = PendingFileOp.OpType.Add,
                Path = relativePath,
                Content = content
            });

            return true;
        }

        public bool Add(IEnumerable<string> paths)
        {
            var addedAny = false;
            foreach (var path in paths)
            {
                if (Add(path))
                {
                    addedAny = true;
                }
            }
            return addedAny;
        }

        public bool AddAll()
        {
            // CRITICAL: Clear pending ops first in case of retry after failure
            pendingOps.Clear();

            var addedAny = false;
            var gitDir = Path.Combine(repoPath, ".git");

            foreach (var file in Directory.GetFiles(repoPath, "*", SearchOption.AllDirectories))
            {
                if (file.StartsWith(gitDir, StringComparison.OrdinalIgnoreCase))
                {
                    continue; // Skip .git folder
                }

                if (Add(file))
                {
                    addedAny = true;
                }
            }

            return addedAny;
        }

        public void Remove(string path, bool recursive)
        {
            var relativePath = GetRelativePath(repoPath, path);
            pendingOps.Add(new PendingFileOp
            {
                Type = PendingFileOp.OpType.Delete,
                Path = relativePath
            });
        }

        public void Move(string sourcePath, string destPath)
        {
            var relativeSource = GetRelativePath(repoPath, sourcePath);
            var relativeDest = GetRelativePath(repoPath, destPath);

            pendingOps.Add(new PendingFileOp
            {
                Type = PendingFileOp.OpType.Rename,
                SourcePath = relativeSource,
                Path = relativeDest
            });
        }

        public bool Commit(string authorName, string authorEmail, string comment, DateTime localTime)
        {
            if (pendingOps.Count == 0)
            {
                return false;
            }

            perfTracker.StartCommit();

            // CRITICAL: Validate author name is not empty
            if (string.IsNullOrWhiteSpace(authorName))
            {
                authorName = "Unknown";
                logger.WriteLine("WARNING: Empty author name, using 'Unknown'");
            }

            // Convert to UTC and format as seconds since epoch
            var utcTime = TimeZoneInfo.ConvertTimeToUtc(localTime);
            var unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var secondsSinceEpoch = (long)(utcTime - unixEpoch).TotalSeconds;
            var timestamp = $"{secondsSinceEpoch} +0000";

            // Write commit header
            WriteLine($"commit refs/heads/master");
            WriteLine($"mark :{markCounter}");

            markCounter++;

            WriteLine($"author {FormatIdentity(authorName, authorEmail)} {timestamp}");
            WriteLine($"committer {FormatIdentity(authorName, authorEmail)} {timestamp}");

            // CRITICAL: Commit messages must use UTF-8 to match the 'data' length encoding
            var commitCommentBytes = Encoding.UTF8.GetBytes(comment ?? "");
            if (commitCount < 5 || commitCount == 181)
            {
                logger.WriteLine($"DEBUG: Commit {commitCount} comment length={commitCommentBytes.Length} bytes first 50: {string.Join(" ", commitCommentBytes.Take(50).Select(b => b.ToString("X2")))}");
            }
            WriteData(commitCommentBytes);

            // CRITICAL: Reference parent commit AFTER data (except for the very first commit)
            if (markCounter > 2)
            {
                WriteLine($"from :{markCounter - 2}");
            }

            // Write file modifications
            foreach (var op in pendingOps)
            {
                switch (op.Type)
                {
                    case PendingFileOp.OpType.Add:
                        WriteFileModification(op.Path, op.Content);
                        break;
                    case PendingFileOp.OpType.Delete:
                        WriteLine($"D {QuotePath(op.Path)}");
                        break;
                    case PendingFileOp.OpType.Rename:
                        WriteLine($"R {QuotePath(op.SourcePath)} {QuotePath(op.Path)}");
                        break;
                }
            }

            // CRITICAL: Write blank line to end commit
            WriteRaw("\n");

            // CRITICAL: Flush after every commit so git can process it
            perfTracker.StartFlush();
            fastImportStream.Flush();
            perfTracker.EndFlush();

            var fileCount = pendingOps.Count;
            var fileBytes = pendingOps.Sum(op => op.Content?.Length ?? 0);

            pendingOps.Clear();
            commitCount++;

            perfTracker.EndCommit(fileCount, fileBytes);

            return true;
        }

        public void Tag(string name, string taggerName, string taggerEmail, string comment, DateTime localTime)
        {
            // CRITICAL: Validate tagger name is not empty
            if (string.IsNullOrWhiteSpace(taggerName))
            {
                taggerName = "Unknown";
                logger.WriteLine($"WARNING: Empty tagger name for tag '{name}', using 'Unknown'");
            }

            // Convert to UTC and format as seconds since epoch
            var utcTime = TimeZoneInfo.ConvertTimeToUtc(localTime);
            var unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var secondsSinceEpoch = (long)(utcTime - unixEpoch).TotalSeconds;
            var timestamp = $"{secondsSinceEpoch} +0000";

            // Write tag command
            WriteLine($"tag {name}");
            WriteLine($"from :{markCounter - 1}"); // Reference last commit
            WriteLine($"tagger {FormatIdentity(taggerName, taggerEmail)} {timestamp}");

            // CRITICAL: Tag messages must use UTF-8, not commitEncoding
            var tagCommentBytes = Encoding.UTF8.GetBytes(comment ?? "");
            logger.WriteLine($"DEBUG: Tag {name} comment='{comment}' length={tagCommentBytes.Length} bytes: {string.Join(" ", tagCommentBytes.Select(b => b.ToString("X2")))}");
            WriteData(tagCommentBytes);
            // Note: Tags do NOT need a blank line after data, unlike commits

            logger.WriteLine($"Created tag: {name}");
        }

        public void Dispose()
        {
            logger.WriteLine($"");
            logger.WriteLine($"Finalizing fast-import: {commitCount} commits processed");

            if (fastImportStream != null)
            {
                try
                {
                    // Write "done" command to signal end of import
                    WriteLine("done");
                    fastImportStream.Flush();
                    fastImportStream.Close();
                }
                catch (Exception ex)
                {
                    logger.WriteLine($"ERROR closing fast-import writer: {ex.Message}");
                }
            }

            if (fastImportProcess != null)
            {
                logger.WriteLine("Waiting for git fast-import to complete...");

                if (!fastImportProcess.WaitForExit(120000)) // 2 minute timeout
                {
                    logger.WriteLine("WARNING: git fast-import did not exit in 120s, killing process");
                    fastImportProcess.Kill();
                }
                else
                {
                    logger.WriteLine($"Git fast-import completed in {stopwatch.Elapsed}");
                }

                // Check for errors
                if (stderrBuffer.Length > 0)
                {
                    logger.WriteLine("Fast-import stderr output:");
                    logger.WriteLine(stderrBuffer.ToString());
                }

                if (fastImportProcess.ExitCode != 0)
                {
                    throw new Exception($"git fast-import failed with exit code {fastImportProcess.ExitCode}");
                }

                fastImportProcess.Dispose();
            }

            stopwatch.Stop();

            // Apply config settings
            if (postImportConfigs != null && postImportConfigs.Count > 0)
            {
                logger.WriteLine("Applying git config settings...");
                foreach (var kvp in postImportConfigs)
                {
                    RunGitCommand($"config {kvp.Key} \"{kvp.Value}\"");
                }
            }

            // CRITICAL: Post-import cleanup for correct working tree
            logger.WriteLine("Running post-import cleanup...");

            // Update HEAD to point to master branch (fix for main/master mismatch)
            RunGitCommand("symbolic-ref HEAD refs/heads/master");

            // Update index to match HEAD
            RunGitCommand("reset --hard HEAD");

            // Optional: Run garbage collection (can take a long time on large repos)
            // Uncomment if you want aggressive optimization at the cost of time
            // logger.WriteLine("Starting git gc (this may take several minutes)...");
            // var gcStart = Stopwatch.StartNew();
            // RunGitCommand("gc --aggressive");
            // logger.WriteLine($"Git gc completed in {gcStart.Elapsed}");

            logger.WriteLine($"");
            logger.WriteLine($"==== Fast-import total time: {stopwatch.Elapsed} ====");
            logger.WriteLine($"");
        }

        // ===== Helper Methods =====

        private void WriteFileModification(string path, byte[] content)
        {
            WriteLine($"M 100644 inline {QuotePath(path)}");
            WriteData(content);
        }

        private void WriteData(string text)
        {
            var bytes = commitEncoding.GetBytes(text);
            WriteData(bytes);
        }

        private void WriteData(byte[] data)
        {
            perfTracker.StartWrite();

            // Write "data {length}\n"
            WriteLine($"data {data.Length}");

            // DEBUG: Log what we're about to write
            if (data.Length <= 20)
            {
                logger.WriteLine($"DEBUG WriteData: Writing {data.Length} bytes: {string.Join(" ", data.Select(b => b.ToString("X2")))}");
            }

            // Write binary data directly
            if (data.Length > 0)
            {
                fastImportStream.Write(data, 0, data.Length);
            }

            // Write terminating newline
            WriteRaw("\n");

            // CRITICAL: Must flush after each data block so git can read it synchronously
            fastImportStream.Flush();

            perfTracker.EndWrite();
        }

        private void WriteLine(string line)
        {
            perfTracker.StartWrite();
            WriteRaw(line + "\n");
            perfTracker.EndWrite();
        }

        // Write raw bytes to stream (all writes go through here)
        private void WriteRaw(string text)
        {
            // CRITICAL: Always use UTF-8 with UNIX line endings
            var bytes = Encoding.UTF8.GetBytes(text);

            // DEBUG: Log all writes to help diagnose issues
            if (commitCount < 3 || text.StartsWith("commit") || text.StartsWith("mark") || text.StartsWith("from"))
            {
                var preview = text.Length > 80 ? text.Substring(0, 80) + "..." : text;
                preview = preview.Replace("\n", "\\n").Replace("\r", "\\r");
                logger.WriteLine($"WRITE[{bytes.Length}]: {preview}");
            }

            fastImportStream.Write(bytes, 0, bytes.Length);
        }

        private string FormatIdentity(string name, string email)
        {
            // Quote name if it contains angle brackets
            if (name.Contains('<') || name.Contains('>'))
            {
                name = "\"" + name.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
            }
            return $"{name} <{email}>";
        }

        private string QuotePath(string path)
        {
            // Replace backslashes with forward slashes
            path = path.Replace('\\', '/');

            // Quote if contains spaces, quotes, or non-ASCII
            if (path.Any(c => c < 32 || c > 126 || c == '"' || c == ' '))
            {
                return "\"" + path.Replace("\"", "\\\"") + "\"";
            }
            return path;
        }

        private string GetRelativePath(string fromPath, string toPath)
        {
            if (string.IsNullOrEmpty(fromPath)) return toPath;
            if (string.IsNullOrEmpty(toPath)) return toPath;

            var fromUri = new Uri(AppendDirectorySeparator(fromPath));
            var toUri = new Uri(toPath);

            if (fromUri.Scheme != toUri.Scheme)
            {
                return toPath; // Paths with different schemes can't be relative
            }

            var relativeUri = fromUri.MakeRelativeUri(toUri);
            var relativePath = Uri.UnescapeDataString(relativeUri.ToString());

            return relativePath.Replace('/', Path.DirectorySeparatorChar);
        }

        private string AppendDirectorySeparator(string path)
        {
            if (!path.EndsWith(Path.DirectorySeparatorChar.ToString()) &&
                !path.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
            {
                return path + Path.DirectorySeparatorChar;
            }
            return path;
        }

        private void RunGitCommand(string arguments)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = repoPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = Process.Start(psi))
            {
                if (process != null)
                {
                    process.WaitForExit();
                    if (process.ExitCode != 0)
                    {
                        var error = process.StandardError.ReadToEnd();
                        logger.WriteLine($"WARNING: git {arguments} failed: {error}");
                    }
                }
            }
        }

        private void StartErrorReader()
        {
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    string line;
                    while ((line = fastImportProcess.StandardError.ReadLine()) != null)
                    {
                        stderrBuffer.AppendLine(line);
                        logger.WriteLine($"fast-import: {line}");
                    }
                }
                catch (Exception ex)
                {
                    logger.WriteLine($"Error reading fast-import stderr: {ex.Message}");
                }
            });
        }

        // ===== Inner Classes =====

        private class PendingFileOp
        {
            public enum OpType { Add, Delete, Rename }
            public OpType Type { get; set; }
            public string Path { get; set; }
            public string SourcePath { get; set; } // for renames
            public byte[] Content { get; set; } // for adds
        }

        /// <summary>
        /// Detailed performance tracking to identify bottlenecks
        /// </summary>
        private class PerformanceTracker
        {
            private readonly Logger logger;
            private readonly Stopwatch overallTimer = Stopwatch.StartNew();
            private int commitCount = 0;

            // Timing buckets - track time spent in each phase
            private long writeTimeMs = 0;      // Time writing to git stdin
            private long flushTimeMs = 0;      // Time waiting for flush
            private long fileReadTimeMs = 0;   // Time reading files from disk

            // Per-commit timing
            private readonly Stopwatch commitTimer = new Stopwatch();
            private readonly Stopwatch writeTimer = new Stopwatch();
            private readonly Stopwatch flushTimer = new Stopwatch();
            private readonly Stopwatch fileReadTimer = new Stopwatch();

            // Statistics
            private long totalFileBytes = 0;
            private int totalFileCount = 0;

            // Rolling window to detect degradation
            private readonly Queue<long> last100CommitTimes = new Queue<long>();

            public PerformanceTracker(Logger logger)
            {
                this.logger = logger;
            }

            public void StartCommit()
            {
                commitTimer.Restart();
            }

            public void StartWrite()
            {
                writeTimer.Restart();
            }

            public void EndWrite()
            {
                writeTimer.Stop();
                writeTimeMs += writeTimer.ElapsedMilliseconds;
            }

            public void StartFlush()
            {
                flushTimer.Restart();
            }

            public void EndFlush()
            {
                flushTimer.Stop();
                flushTimeMs += flushTimer.ElapsedMilliseconds;
            }

            public void StartFileRead()
            {
                fileReadTimer.Restart();
            }

            public void EndFileRead()
            {
                fileReadTimer.Stop();
                fileReadTimeMs += fileReadTimer.ElapsedMilliseconds;
            }

            public void EndCommit(int fileCount, long fileBytes)
            {
                commitTimer.Stop();
                commitCount++;
                totalFileCount += fileCount;
                totalFileBytes += fileBytes;

                var commitTimeMs = commitTimer.ElapsedMilliseconds;

                // Track rolling window
                last100CommitTimes.Enqueue(commitTimeMs);
                if (last100CommitTimes.Count > 100)
                {
                    last100CommitTimes.Dequeue();
                }

                // Log abnormally slow commits immediately
                if (commitTimeMs > 1000)
                {
                    logger.WriteLine($"WARNING: SLOW COMMIT #{commitCount}: {commitTimeMs}ms");
                    logger.WriteLine($"  Files: {fileCount}, Bytes: {fileBytes}");
                    logger.WriteLine($"  Write: {writeTimer.ElapsedMilliseconds}ms");
                    logger.WriteLine($"  Flush: {flushTimer.ElapsedMilliseconds}ms");
                    logger.WriteLine($"  FileRead: {fileReadTimer.ElapsedMilliseconds}ms");
                }

                // Detailed stats every 100 commits
                if (commitCount % 100 == 0)
                {
                    var totalSecs = overallTimer.Elapsed.TotalSeconds;
                    var avgCommitTime = totalSecs * 1000 / commitCount;
                    var recentAvg = last100CommitTimes.Average();
                    var commitsPerSec = commitCount / totalSecs;

                    // Calculate percentage breakdown
                    var totalTrackedMs = writeTimeMs + flushTimeMs + fileReadTimeMs;
                    var writePct = totalTrackedMs > 0 ? (writeTimeMs * 100.0 / totalTrackedMs) : 0;
                    var flushPct = totalTrackedMs > 0 ? (flushTimeMs * 100.0 / totalTrackedMs) : 0;
                    var fileReadPct = totalTrackedMs > 0 ? (fileReadTimeMs * 100.0 / totalTrackedMs) : 0;
                    var otherMs = totalSecs * 1000 - totalTrackedMs;

                    logger.WriteLine($"");
                    logger.WriteLine($"=== Performance Stats: {commitCount} commits in {overallTimer.Elapsed} ===");
                    logger.WriteLine($"  Rate: {commitsPerSec:F2} commits/sec ({commitsPerSec * 60:F1} commits/min)");
                    logger.WriteLine($"  Avg commit time: {avgCommitTime:F1}ms (overall), {recentAvg:F1}ms (last 100)");

                    // Detect performance degradation
                    if (commitCount > 100 && recentAvg > avgCommitTime * 1.5)
                    {
                        logger.WriteLine($"  *** PERFORMANCE DEGRADATION DETECTED! ***");
                        logger.WriteLine($"  Recent commits are {(recentAvg / avgCommitTime):F2}x slower than average");
                    }

                    logger.WriteLine($"  Time breakdown:");
                    logger.WriteLine($"    Writing to git: {writeTimeMs}ms ({writePct:F1}%)");
                    logger.WriteLine($"    Flushing: {flushTimeMs}ms ({flushPct:F1}%)");
                    logger.WriteLine($"    Reading files: {fileReadTimeMs}ms ({fileReadPct:F1}%)");
                    logger.WriteLine($"    Other: {otherMs:F0}ms");

                    var memoryMB = GC.GetTotalMemory(false) / 1024 / 1024;
                    var gen0 = GC.CollectionCount(0);
                    var gen1 = GC.CollectionCount(1);
                    var gen2 = GC.CollectionCount(2);
                    logger.WriteLine($"  Memory: {memoryMB}MB (Gen0: {gen0}, Gen1: {gen1}, Gen2: {gen2})");

                    logger.WriteLine($"  Avg files/commit: {totalFileCount / (double)commitCount:F1}");
                    logger.WriteLine($"  Total data written: {totalFileBytes / 1024 / 1024}MB");
                    logger.WriteLine($"");
                }
            }
        }
    }
}
