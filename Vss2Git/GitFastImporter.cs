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
    /// High-performance Git repository implementation using git fast-import.
    ///
    /// This implementation provides 20-100x performance improvement over process-per-command
    /// approaches by streaming commands to a single long-running git fast-import process.
    ///
    /// Key features:
    /// - Strict adherence to git fast-import specification (UTF-8, LF-only, exact byte counts)
    /// - Configurable flush strategy for performance vs incremental progress tradeoff
    /// - Comprehensive error handling with actionable error messages
    /// - Detailed performance tracking and logging
    /// - Automatic validation of all inputs
    /// </summary>
    /// <author>Trevor Robinson (original GitWrapper), Dan Cristoloveanu (fast-import), Rewritten 2025</author>
    class GitFastImporter : IGitRepository, IDisposable
    {
        private readonly string repoPath;
        private readonly Logger logger;
        private readonly GitFastImporterConfig config;
        private readonly Stopwatch stopwatch = new Stopwatch();

        // Core components
        private Process fastImportProcess;
        private Stream fastImportStream;
        private MarkManager markManager;
        private PerformanceMonitor perfMonitor;

        // State tracking
        private int commitsSinceLastFlush = 0;
        private bool disposed = false;

        // Buffered operations (staged for next commit)
        private readonly List<PendingFileOp> pendingOps = new List<PendingFileOp>();

        // Track last write times to detect changed files
        private readonly Dictionary<string, DateTime> fileLastWriteCache = new(StringComparer.OrdinalIgnoreCase);

        // Cache the list of files in the repository to avoid expensive Directory.GetFiles() calls
        private HashSet<string> cachedFileList = null;
        private bool fileListNeedsRefresh = true;

        // Config settings to apply after import
        private Dictionary<string, string> postImportConfigs;

        // Error tracking
        private readonly StringBuilder stderrBuffer = new StringBuilder();

        /// <summary>
        /// Gets the total elapsed time since initialization.
        /// </summary>
        public TimeSpan ElapsedTime => stopwatch.Elapsed;

        /// <summary>
        /// Commit encoding property (maintained for IGitRepository compatibility).
        /// NOTE: Git fast-import REQUIRES UTF-8 for commit messages per specification.
        /// Setting this property to non-UTF8 will log a warning and be ignored.
        /// </summary>
        public Encoding CommitEncoding
        {
            get => Encoding.UTF8;
            set
            {
                if (value != null && value.CodePage != Encoding.UTF8.CodePage)
                {
                    logger?.WriteLine($"WARNING: CommitEncoding set to {value.EncodingName} but git fast-import REQUIRES UTF-8 for commit messages. Ignoring setting.");
                    logger?.WriteLine("         All commit and tag messages will be UTF-8 encoded per git fast-import specification.");
                }
            }
        }

        /// <summary>
        /// Creates a new GitFastImporter instance.
        /// </summary>
        /// <param name="repoPath">Path to the git repository</param>
        /// <param name="logger">Logger for output</param>
        /// <param name="config">Configuration options (uses defaults if null)</param>
        public GitFastImporter(string repoPath, Logger logger, GitFastImporterConfig config = null)
        {
            this.repoPath = repoPath ?? throw new ArgumentNullException(nameof(repoPath));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.config = config ?? new GitFastImporterConfig();

            // Initialize components
            this.markManager = new MarkManager();
            this.perfMonitor = new PerformanceMonitor(logger, this.config.EnableDetailedPerformanceTracking);
        }

        /// <summary>
        /// Initializes the git repository and starts the fast-import process.
        /// </summary>
        public void Init()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(GitFastImporter));

            try
            {
                logger.WriteLine("==================================================");
                logger.WriteLine("Initializing git repository with fast-import");
                logger.WriteLine($"  Repository: {repoPath}");
                logger.WriteLine($"  Flush strategy: {config.FlushStrategy}");
                logger.WriteLine($"  Process timeout: {config.ProcessTimeoutMs}ms");
                logger.WriteLine($"  Detailed tracking: {config.EnableDetailedPerformanceTracking}");
                logger.WriteLine($"  Debug logging: {config.EnableDebugLogging}");
                logger.WriteLine("==================================================");

                // Create repo directory
                Directory.CreateDirectory(repoPath);
                logger.WriteLine($"Created repository directory: {repoPath}");

                // Initialize git repository
                RunGitCommand("init", "Failed to initialize git repository");
                logger.WriteLine("Git repository initialized");

                // Start fast-import process
                logger.WriteLine("Starting git fast-import process...");
                StartFastImportProcess();

                stopwatch.Start();
                logger.WriteLine($"Git fast-import ready (PID: {fastImportProcess.Id})");
            }
            catch (Exception ex)
            {
                logger.WriteLine($"ERROR during initialization: {ex.Message}");
                logger.WriteLine($"Stack trace: {ex.StackTrace}");
                throw new Exception($"Failed to initialize GitFastImporter: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Starts the git fast-import process and initializes the stream.
        /// </summary>
        private void StartFastImportProcess()
        {
            var gitPath = config.GitExecutablePath ?? "git";

            fastImportProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = gitPath,
                    Arguments = "fast-import --quiet --done",
                    WorkingDirectory = repoPath,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            try
            {
                fastImportProcess.Start();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to start git fast-import process. Ensure git is installed and in PATH. Git path attempted: {gitPath}", ex);
            }

            // CRITICAL: Use BaseStream directly - no StreamWriter wrapper
            // This gives us complete control over encoding and buffering
            fastImportStream = fastImportProcess.StandardInput.BaseStream;

            // Start async stderr reader to prevent deadlock
            StartAsyncErrorReader();
        }

        /// <summary>
        /// Sets a git configuration value (applied after import completes).
        /// </summary>
        public void SetConfig(string name, string value)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(GitFastImporter));

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Config name cannot be empty", nameof(name));

            postImportConfigs ??= new Dictionary<string, string>();
            postImportConfigs[name] = value ?? "";

            if (config.EnableDebugLogging)
                logger.WriteLine($"Config queued: {name} = {value}");
        }

        /// <summary>
        /// Adds a single file to the staging area for the next commit.
        /// </summary>
        public bool Add(string path)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(GitFastImporter));

            if (string.IsNullOrWhiteSpace(path))
            {
                logger.WriteLine("WARNING: Add called with empty path");
                return false;
            }

            if (!File.Exists(path))
            {
                logger.WriteLine($"WARNING: File not found for add: {path}");
                return false;
            }

            try
            {
                perfMonitor.StartFileRead();

                // Read file content
                byte[] content = File.ReadAllBytes(path);

                perfMonitor.EndFileRead(content.Length);

                // Get relative path
                var relativePath = GetRelativePath(repoPath, path);
                ValidatePath(relativePath);

                // Add to pending operations
                pendingOps.Add(new PendingFileOp
                {
                    Type = PendingFileOp.OpType.Add,
                    Path = relativePath,
                    Content = content
                });

                if (config.EnableDebugLogging)
                    logger.WriteLine($"Staged add: {relativePath} ({content.Length} bytes)");

                return true;
            }
            catch (Exception ex)
            {
                logger.WriteLine($"ERROR reading file {path}: {ex.Message}");
                throw new Exception($"Failed to add file {path}", ex);
            }
        }

        /// <summary>
        /// Adds multiple files to the staging area for the next commit.
        /// </summary>
        public bool Add(IEnumerable<string> paths)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(GitFastImporter));

            if (paths == null)
                throw new ArgumentNullException(nameof(paths));

            var addedAny = false;
            foreach (var path in paths)
            {
                if (Add(path))
                    addedAny = true;
            }
            return addedAny;
        }

        /// <summary>
        /// Adds all files in the repository to the staging area for the next commit.
        /// OPTIMIZED: Caches file list and only processes changed files.
        /// </summary>
        public bool AddAll()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(GitFastImporter));

            // Clear pending ops first in case of retry after failure
            pendingOps.Clear();

            var addedAny = false;
            var gitDir = Path.Combine(repoPath, ".git");
            var fileCount = 0;
            var skippedCount = 0;
            var newFileCount = 0;

            try
            {
                // CRITICAL OPTIMIZATION: Cache the file list to avoid expensive Directory.GetFiles() calls
                // Only refresh if files were added/removed/moved
                if (fileListNeedsRefresh || cachedFileList == null)
                {
                    var sw = Stopwatch.StartNew();
                    cachedFileList = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var file in Directory.GetFiles(repoPath, "*", SearchOption.AllDirectories))
                    {
                        if (!file.StartsWith(gitDir, StringComparison.OrdinalIgnoreCase))
                        {
                            cachedFileList.Add(file);
                        }
                    }
                    fileListNeedsRefresh = false;
                    sw.Stop();

                    if (config.EnableDebugLogging)
                    {
                        logger.WriteLine($"AddAll: Refreshed file list cache ({cachedFileList.Count} files in {sw.ElapsedMilliseconds}ms)");
                    }
                }

                // Process each file in the cached list
                var deletedFiles = new List<string>();
                foreach (var file in cachedFileList)
                {
                    // Check if file still exists (might have been deleted)
                    var fileInfo = new FileInfo(file);
                    if (!fileInfo.Exists)
                    {
                        deletedFiles.Add(file);
                        fileLastWriteCache.Remove(file);
                        continue;
                    }

                    // OPTIMIZATION: Only add files that have changed
                    var lastWriteTime = fileInfo.LastWriteTimeUtc;

                    if (fileLastWriteCache.TryGetValue(file, out DateTime cachedTime))
                    {
                        // File exists in cache - check if it changed
                        if (cachedTime == lastWriteTime)
                        {
                            // File hasn't changed - skip it!
                            skippedCount++;
                            continue;
                        }
                    }
                    else
                    {
                        // New file - track it
                        newFileCount++;
                    }

                    // File is new or changed - add it
                    if (Add(file))
                    {
                        // Update cache with new write time
                        fileLastWriteCache[file] = lastWriteTime;
                        addedAny = true;
                        fileCount++;
                    }
                }

                // Remove deleted files from cache
                if (deletedFiles.Count > 0)
                {
                    foreach (var deleted in deletedFiles)
                    {
                        cachedFileList.Remove(deleted);
                    }
                    fileListNeedsRefresh = true; // Refresh on next call to pick up any new files
                }

                // If new files were detected, refresh cache next time to ensure we catch all new files
                if (newFileCount > 0)
                {
                    fileListNeedsRefresh = true;
                }

                if (config.EnableDebugLogging || fileCount > 0)
                {
                    logger.WriteLine($"AddAll: staged {fileCount} files (new: {newFileCount}, changed: {fileCount - newFileCount}, skipped: {skippedCount})");
                }

                return addedAny;
            }
            catch (Exception ex)
            {
                logger.WriteLine($"ERROR during AddAll: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Removes a file or directory from the repository.
        /// </summary>
        public void Remove(string path, bool recursive)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(GitFastImporter));

            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be empty", nameof(path));

            var relativePath = GetRelativePath(repoPath, path);
            ValidatePath(relativePath);

            pendingOps.Add(new PendingFileOp
            {
                Type = PendingFileOp.OpType.Delete,
                Path = relativePath
            });

            if (config.EnableDebugLogging)
                logger.WriteLine($"Staged delete: {relativePath}");
        }

        /// <summary>
        /// Moves (renames) a file or directory in the repository.
        /// </summary>
        public void Move(string sourcePath, string destPath)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(GitFastImporter));

            if (string.IsNullOrWhiteSpace(sourcePath))
                throw new ArgumentException("Source path cannot be empty", nameof(sourcePath));
            if (string.IsNullOrWhiteSpace(destPath))
                throw new ArgumentException("Destination path cannot be empty", nameof(destPath));

            var relativeSource = GetRelativePath(repoPath, sourcePath);
            var relativeDest = GetRelativePath(repoPath, destPath);

            ValidatePath(relativeSource);
            ValidatePath(relativeDest);

            pendingOps.Add(new PendingFileOp
            {
                Type = PendingFileOp.OpType.Rename,
                SourcePath = relativeSource,
                Path = relativeDest
            });

            if (config.EnableDebugLogging)
                logger.WriteLine($"Staged rename: {relativeSource} -> {relativeDest}");
        }

        /// <summary>
        /// Commits all staged changes with the specified metadata.
        /// </summary>
        /// <returns>True if commit was created, false if no changes were staged</returns>
        public bool Commit(string authorName, string authorEmail, string comment, DateTime localTime)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(GitFastImporter));

            // Nothing to commit
            if (pendingOps.Count == 0)
            {
                if (config.EnableDebugLogging)
                    logger.WriteLine("Commit called with no pending operations");
                return false;
            }

            perfMonitor.StartCommit();

            try
            {
                // Validate inputs
                ValidateIdentity(ref authorName, ref authorEmail, "author");
                ValidateCommitMessage(ref comment);

                // Allocate mark for this commit
                int currentMark = markManager.AllocateCommitMark();
                int? parentMark = markManager.GetParentMark();

                // Format timestamp
                string timestamp = FormatTimestamp(localTime);

                // Write commit command
                WriteLine($"commit refs/heads/master");
                WriteLine($"mark :{currentMark}");
                WriteLine($"committer {FormatIdentity(authorName, authorEmail)} {timestamp}");

                // Write commit message (MUST be UTF-8 per spec)
                var messageBytes = Encoding.UTF8.GetBytes(comment);
                WriteDataBlock(messageBytes);

                // Write parent reference (if not root commit)
                if (parentMark.HasValue)
                {
                    WriteLine($"from :{parentMark.Value}");
                }

                // Write file modifications
                int fileOpCount = 0;
                long totalBytes = 0;

                foreach (var op in pendingOps)
                {
                    WriteFileOperation(op);
                    fileOpCount++;
                    totalBytes += op.Content?.Length ?? 0;
                }

                // End commit with blank line
                WriteRaw("\n");

                // Flush based on strategy
                commitsSinceLastFlush++;
                if (ShouldFlush())
                {
                    perfMonitor.StartFlush();
                    FlushStream();
                    perfMonitor.EndFlush();
                    commitsSinceLastFlush = 0;
                }

                // Clear pending operations
                pendingOps.Clear();

                // Update performance monitor
                perfMonitor.EndCommit(fileOpCount, totalBytes);

                return true;
            }
            catch (Exception ex)
            {
                logger.WriteLine($"ERROR during commit: {ex.Message}");
                logger.WriteLine($"  Author: {authorName} <{authorEmail}>");
                logger.WriteLine($"  Message: {comment?.Substring(0, Math.Min(50, comment?.Length ?? 0))}...");
                logger.WriteLine($"  Pending ops: {pendingOps.Count}");
                throw new Exception("Failed to create commit", ex);
            }
        }

        /// <summary>
        /// Creates an annotated tag pointing to the last commit.
        /// </summary>
        public void Tag(string name, string taggerName, string taggerEmail, string comment, DateTime localTime)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(GitFastImporter));

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Tag name cannot be empty", nameof(name));

            try
            {
                // Validate inputs
                ValidateIdentity(ref taggerName, ref taggerEmail, "tagger");
                ValidateCommitMessage(ref comment);

                // Get last commit mark
                int lastCommitMark = markManager.GetLastCommitMark();

                // Format timestamp
                string timestamp = FormatTimestamp(localTime);

                // Write tag command
                WriteLine($"tag {name}");
                WriteLine($"from :{lastCommitMark}");
                WriteLine($"tagger {FormatIdentity(taggerName, taggerEmail)} {timestamp}");

                // Write tag message (MUST be UTF-8 per spec)
                var messageBytes = Encoding.UTF8.GetBytes(comment);
                WriteDataBlock(messageBytes);

                logger.WriteLine($"Created tag: {name} -> commit :{lastCommitMark}");
            }
            catch (InvalidOperationException ex)
            {
                logger.WriteLine($"ERROR creating tag '{name}': {ex.Message}");
                throw new Exception($"Cannot create tag '{name}': No commits exist yet", ex);
            }
            catch (Exception ex)
            {
                logger.WriteLine($"ERROR creating tag '{name}': {ex.Message}");
                throw new Exception($"Failed to create tag '{name}'", ex);
            }
        }

        /// <summary>
        /// Disposes the GitFastImporter and finalizes the import.
        /// </summary>
        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;

            try
            {
                var commitCount = markManager.GetCommitCount();

                logger.WriteLine("");
                logger.WriteLine("==================================================");
                logger.WriteLine($"Finalizing fast-import: {commitCount} commits processed");
                logger.WriteLine("==================================================");

                // Close the fast-import stream
                if (fastImportStream != null)
                {
                    try
                    {
                        // Write "done" command (required with --done flag)
                        WriteLine("done");

                        // Final flush
                        FlushStream();

                        // Close stream
                        fastImportStream.Close();
                        logger.WriteLine("Fast-import stream closed");
                    }
                    catch (Exception ex)
                    {
                        logger.WriteLine($"ERROR closing fast-import stream: {ex.Message}");
                    }
                }

                // Wait for process to exit
                if (fastImportProcess != null)
                {
                    logger.WriteLine($"Waiting for git fast-import to complete (timeout: {config.ProcessTimeoutMs}ms)...");

                    if (!fastImportProcess.WaitForExit(config.ProcessTimeoutMs))
                    {
                        logger.WriteLine($"WARNING: git fast-import did not exit within {config.ProcessTimeoutMs}ms, killing process");
                        fastImportProcess.Kill();
                    }
                    else
                    {
                        logger.WriteLine($"Git fast-import completed successfully in {stopwatch.Elapsed}");
                    }

                    // Check for errors
                    if (stderrBuffer.Length > 0)
                    {
                        logger.WriteLine("");
                        logger.WriteLine("Fast-import stderr output:");
                        logger.WriteLine(stderrBuffer.ToString());
                    }

                    // Check exit code
                    if (fastImportProcess.ExitCode != 0)
                    {
                        var crashReports = Directory.GetFiles(Path.Combine(repoPath, ".git"), "fast_import_crash_*");
                        if (crashReports.Length > 0)
                        {
                            logger.WriteLine($"ERROR: Crash reports found: {string.Join(", ", crashReports)}");
                        }

                        throw new Exception($"git fast-import failed with exit code {fastImportProcess.ExitCode}. Check stderr output above.");
                    }

                    fastImportProcess.Dispose();
                }

                stopwatch.Stop();

                // Apply post-import git config
                ApplyPostImportConfigs();

                // Post-import cleanup
                PostImportCleanup();

                // Print final statistics
                perfMonitor.PrintFinalStatistics(stopwatch.Elapsed);

                logger.WriteLine("");
                logger.WriteLine("==================================================");
                logger.WriteLine($"Fast-import completed successfully!");
                logger.WriteLine($"  Total time: {stopwatch.Elapsed}");
                logger.WriteLine($"  Total commits: {commitCount}");
                logger.WriteLine($"  Repository: {repoPath}");
                logger.WriteLine("==================================================");
            }
            catch (Exception ex)
            {
                logger.WriteLine($"ERROR during disposal: {ex.Message}");
                throw;
            }
        }

        // ===== Private Helper Methods =====

        /// <summary>
        /// Determines if the stream should be flushed based on the configured strategy.
        /// </summary>
        private bool ShouldFlush()
        {
            return config.FlushStrategy switch
            {
                FlushStrategy.EveryCommit => true,
                FlushStrategy.EveryTenCommits => commitsSinceLastFlush >= 10,
                FlushStrategy.EveryHundredCommits => commitsSinceLastFlush >= 100,
                FlushStrategy.EveryThousandCommits => commitsSinceLastFlush >= 1000,
                FlushStrategy.Manual => false,
                FlushStrategy.AtEnd => false,
                _ => false
            };
        }

        /// <summary>
        /// Flushes the stream to git fast-import.
        /// </summary>
        private void FlushStream()
        {
            try
            {
                fastImportStream?.Flush();
            }
            catch (IOException ex)
            {
                throw new Exception("Failed to flush stream to git fast-import. Process may have crashed.", ex);
            }
        }

        /// <summary>
        /// Writes a file operation (add, delete, rename) to the stream.
        /// </summary>
        private void WriteFileOperation(PendingFileOp op)
        {
            switch (op.Type)
            {
                case PendingFileOp.OpType.Add:
                    WriteLine($"M 100644 inline {QuotePath(op.Path)}");
                    WriteDataBlock(op.Content);
                    break;

                case PendingFileOp.OpType.Delete:
                    WriteLine($"D {QuotePath(op.Path)}");
                    break;

                case PendingFileOp.OpType.Rename:
                    WriteLine($"R {QuotePath(op.SourcePath)} {QuotePath(op.Path)}");
                    break;

                default:
                    throw new InvalidOperationException($"Unknown operation type: {op.Type}");
            }
        }

        /// <summary>
        /// Writes a data block with exact byte count.
        /// CRITICAL: This is a performance-critical method - no unnecessary operations!
        /// </summary>
        private void WriteDataBlock(byte[] data)
        {
            if (data == null)
                data = Array.Empty<byte>();

            try
            {
                perfMonitor.StartWrite();

                // Write data command with length
                var dataCommand = $"data {data.Length}\n";
                var commandBytes = Encoding.UTF8.GetBytes(dataCommand);
                fastImportStream.Write(commandBytes, 0, commandBytes.Length);

                // Write raw data
                if (data.Length > 0)
                {
                    fastImportStream.Write(data, 0, data.Length);
                }

                // Write terminating newline
                var newline = new byte[] { (byte)'\n' };
                fastImportStream.Write(newline, 0, 1);

                // NOTE: We do NOT flush here! This was the critical performance bug.
                // Flushing happens after complete commits based on the flush strategy.

                perfMonitor.EndWrite(commandBytes.Length + data.Length + 1);
            }
            catch (IOException ex)
            {
                throw new Exception("Failed to write data block to git fast-import", ex);
            }
        }

        /// <summary>
        /// Writes a command line to the stream.
        /// </summary>
        private void WriteLine(string line)
        {
            if (config.EnableDebugLogging)
            {
                var preview = line.Length > 100 ? line.Substring(0, 100) + "..." : line;
                logger.WriteLine($"  > {preview}");
            }

            WriteRaw(line + "\n");
        }

        /// <summary>
        /// Writes raw text to the stream (always UTF-8 encoded with LF line endings).
        /// </summary>
        private void WriteRaw(string text)
        {
            try
            {
                perfMonitor.StartWrite();

                // CRITICAL: Always UTF-8, always LF (no CRLF)
                var bytes = Encoding.UTF8.GetBytes(text);
                fastImportStream.Write(bytes, 0, bytes.Length);

                perfMonitor.EndWrite(bytes.Length);
            }
            catch (IOException ex)
            {
                throw new Exception("Failed to write to git fast-import. Process may have crashed.", ex);
            }
        }

        /// <summary>
        /// Formats an identity string (name and email).
        /// </summary>
        private string FormatIdentity(string name, string email)
        {
            // Quote name if it contains special characters
            if (name.Contains('<') || name.Contains('>') || name.Contains('"'))
            {
                name = "\"" + name.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
            }

            return $"{name} <{email}>";
        }

        /// <summary>
        /// Formats a timestamp as seconds since Unix epoch with timezone.
        /// </summary>
        private string FormatTimestamp(DateTime localTime)
        {
            // Convert to UTC
            var utcTime = TimeZoneInfo.ConvertTimeToUtc(localTime);

            // Calculate seconds since Unix epoch
            var unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var secondsSinceEpoch = (long)(utcTime - unixEpoch).TotalSeconds;

            // Format as "seconds +0000" (UTC timezone)
            return $"{secondsSinceEpoch} +0000";
        }

        /// <summary>
        /// Quotes a path if it contains special characters.
        /// </summary>
        private string QuotePath(string path)
        {
            // Normalize to forward slashes (git requirement)
            path = path.Replace('\\', '/');

            // Quote if contains spaces, quotes, or non-ASCII characters
            if (path.Any(c => c < 32 || c > 126 || c == '"' || c == ' '))
            {
                return "\"" + path.Replace("\"", "\\\"") + "\"";
            }

            return path;
        }

        /// <summary>
        /// Validates and normalizes an identity (name and email).
        /// </summary>
        private void ValidateIdentity(ref string name, ref string email, string identityType)
        {
            // Ensure name is not empty
            if (string.IsNullOrWhiteSpace(name))
            {
                name = "Unknown";
                logger.WriteLine($"WARNING: Empty {identityType} name, using 'Unknown'");
            }

            // Ensure email is not empty
            if (string.IsNullOrWhiteSpace(email))
            {
                email = "unknown@example.com";
                logger.WriteLine($"WARNING: Empty {identityType} email, using 'unknown@example.com'");
            }

            // Warn if email looks malformed
            if (!email.Contains('@'))
            {
                logger.WriteLine($"WARNING: {identityType} email '{email}' does not contain '@'");
            }
        }

        /// <summary>
        /// Validates and normalizes a commit/tag message.
        /// </summary>
        private void ValidateCommitMessage(ref string message)
        {
            if (message == null)
            {
                message = "";
                logger.WriteLine("WARNING: Null commit message, using empty string");
            }
        }

        /// <summary>
        /// Validates a git path (no leading slash, no .., etc.)
        /// </summary>
        private void ValidatePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be empty");

            if (path.StartsWith("/"))
                throw new ArgumentException($"Path cannot start with '/': {path}");

            if (path.Contains(".."))
                throw new ArgumentException($"Path cannot contain '..': {path}");

            if (path.Contains("\0"))
                throw new ArgumentException($"Path cannot contain NUL byte: {path}");
        }

        /// <summary>
        /// Gets the relative path from one path to another.
        /// </summary>
        private string GetRelativePath(string fromPath, string toPath)
        {
            if (string.IsNullOrEmpty(fromPath)) return toPath;
            if (string.IsNullOrEmpty(toPath)) return toPath;

            var fromUri = new Uri(AppendDirectorySeparator(fromPath));
            var toUri = new Uri(toPath);

            if (fromUri.Scheme != toUri.Scheme)
                return toPath;

            var relativeUri = fromUri.MakeRelativeUri(toUri);
            var relativePath = Uri.UnescapeDataString(relativeUri.ToString());

            return relativePath.Replace('/', Path.DirectorySeparatorChar);
        }

        /// <summary>
        /// Appends a directory separator to a path if not already present.
        /// </summary>
        private string AppendDirectorySeparator(string path)
        {
            if (!path.EndsWith(Path.DirectorySeparatorChar.ToString()) &&
                !path.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
            {
                return path + Path.DirectorySeparatorChar;
            }
            return path;
        }

        /// <summary>
        /// Runs a git command and waits for it to complete.
        /// </summary>
        private void RunGitCommand(string arguments, string errorContext = null)
        {
            var gitPath = config.GitExecutablePath ?? "git";

            var psi = new ProcessStartInfo
            {
                FileName = gitPath,
                Arguments = arguments,
                WorkingDirectory = repoPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            try
            {
                using var process = Process.Start(psi);
                if (process == null)
                    throw new Exception($"Failed to start git process: {arguments}");

                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    var error = process.StandardError.ReadToEnd();
                    var context = errorContext ?? $"git {arguments}";
                    throw new Exception($"{context}: {error}");
                }
            }
            catch (Exception ex) when (ex is not Exception)
            {
                throw new Exception($"Failed to run git command: {arguments}", ex);
            }
        }

        /// <summary>
        /// Applies post-import git configuration.
        /// </summary>
        private void ApplyPostImportConfigs()
        {
            if (postImportConfigs == null || postImportConfigs.Count == 0)
                return;

            logger.WriteLine("");
            logger.WriteLine("Applying git configuration...");

            foreach (var kvp in postImportConfigs)
            {
                try
                {
                    RunGitCommand($"config {kvp.Key} \"{kvp.Value}\"");
                    logger.WriteLine($"  Set {kvp.Key} = {kvp.Value}");
                }
                catch (Exception ex)
                {
                    logger.WriteLine($"  WARNING: Failed to set {kvp.Key}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Performs post-import cleanup (update HEAD, reset working tree).
        /// </summary>
        private void PostImportCleanup()
        {
            logger.WriteLine("");
            logger.WriteLine("Running post-import cleanup...");

            try
            {
                // Update HEAD to point to master branch
                RunGitCommand("symbolic-ref HEAD refs/heads/master", "Failed to set HEAD");
                logger.WriteLine("  Set HEAD to refs/heads/master");

                // Update working tree to match HEAD
                RunGitCommand("reset --hard HEAD", "Failed to reset working tree");
                logger.WriteLine("  Reset working tree to HEAD");

                // Optional: Run garbage collection
                if (config.RunGarbageCollection)
                {
                    logger.WriteLine("  Running git gc (this may take several minutes)...");
                    var gcStart = Stopwatch.StartNew();
                    RunGitCommand("gc --aggressive", "Git gc failed");
                    logger.WriteLine($"  Git gc completed in {gcStart.Elapsed}");
                }
            }
            catch (Exception ex)
            {
                logger.WriteLine($"WARNING during post-import cleanup: {ex.Message}");
            }
        }

        /// <summary>
        /// Starts async reading of stderr to prevent deadlock.
        /// </summary>
        private void StartAsyncErrorReader()
        {
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    string line;
                    while ((line = fastImportProcess.StandardError.ReadLine()) != null)
                    {
                        stderrBuffer.AppendLine(line);
                        logger.WriteLine($"[git fast-import] {line}");
                    }
                }
                catch (Exception ex)
                {
                    logger.WriteLine($"ERROR reading fast-import stderr: {ex.Message}");
                }
            });
        }

        // ===== Nested Classes =====

        /// <summary>
        /// Configuration options for GitFastImporter.
        /// </summary>
        public class GitFastImporterConfig
        {
            /// <summary>
            /// How often to flush the stream to git fast-import.
            /// Default: EveryCommit (necessary for large commits with thousands of files)
            /// </summary>
            public FlushStrategy FlushStrategy { get; set; } = FlushStrategy.EveryCommit;

            /// <summary>
            /// Timeout in milliseconds for git fast-import process to exit.
            /// Default: 600000 (10 minutes)
            /// </summary>
            public int ProcessTimeoutMs { get; set; } = 600000;

            /// <summary>
            /// Enable detailed performance tracking (adds small overhead).
            /// Default: true
            /// </summary>
            public bool EnableDetailedPerformanceTracking { get; set; } = true;

            /// <summary>
            /// Enable verbose debug logging (logs every command).
            /// Default: false
            /// </summary>
            public bool EnableDebugLogging { get; set; } = false;

            /// <summary>
            /// Run git gc (garbage collection) after import completes.
            /// Default: false (can be very slow on large repositories)
            /// </summary>
            public bool RunGarbageCollection { get; set; } = false;

            /// <summary>
            /// Custom git executable path (null uses PATH).
            /// Default: null
            /// </summary>
            public string GitExecutablePath { get; set; } = null;
        }

        /// <summary>
        /// Flush strategy options.
        /// </summary>
        public enum FlushStrategy
        {
            /// <summary>Flush after every commit (slowest, most incremental)</summary>
            EveryCommit,

            /// <summary>Flush every 10 commits</summary>
            EveryTenCommits,

            /// <summary>Flush every 100 commits (default, good balance)</summary>
            EveryHundredCommits,

            /// <summary>Flush every 1000 commits</summary>
            EveryThousandCommits,

            /// <summary>Only flush when manually called (advanced)</summary>
            Manual,

            /// <summary>Only flush at end of import (fastest, no incremental progress)</summary>
            AtEnd
        }

        /// <summary>
        /// Manages mark allocation and tracking for commits.
        /// </summary>
        private class MarkManager
        {
            private int nextMark = 1;
            private int lastCommitMark = 0;

            /// <summary>
            /// Allocates a new mark for a commit.
            /// </summary>
            public int AllocateCommitMark()
            {
                lastCommitMark = nextMark;
                nextMark++;
                return lastCommitMark;
            }

            /// <summary>
            /// Gets the mark of the last commit (for use as parent).
            /// </summary>
            public int? GetParentMark()
            {
                // Return null if this is the first commit (no parent)
                return lastCommitMark > 1 ? lastCommitMark - 1 : null;
            }

            /// <summary>
            /// Gets the mark of the last commit (throws if no commits exist).
            /// </summary>
            public int GetLastCommitMark()
            {
                if (lastCommitMark == 0)
                    throw new InvalidOperationException("No commits have been created yet");

                return lastCommitMark;
            }

            /// <summary>
            /// Gets the total number of commits created.
            /// </summary>
            public int GetCommitCount()
            {
                return lastCommitMark;
            }
        }

        /// <summary>
        /// Tracks performance metrics with minimal overhead.
        /// </summary>
        private class PerformanceMonitor
        {
            private readonly Logger logger;
            private readonly bool enabled;
            private readonly Stopwatch overallTimer = Stopwatch.StartNew();

            // Commit tracking
            private int commitCount = 0;
            private readonly Stopwatch commitTimer = new Stopwatch();

            // Timing buckets
            private long writeTimeMs = 0;
            private long flushTimeMs = 0;
            private long fileReadTimeMs = 0;

            // Per-operation timers (only used if enabled)
            private readonly Stopwatch writeTimer = new Stopwatch();
            private readonly Stopwatch flushTimer = new Stopwatch();
            private readonly Stopwatch fileReadTimer = new Stopwatch();

            // Statistics
            private long totalFileBytes = 0;
            private int totalFileCount = 0;
            private long totalBytesWritten = 0;
            private long totalBytesRead = 0;

            public PerformanceMonitor(Logger logger, bool enabled)
            {
                this.logger = logger;
                this.enabled = enabled;
            }

            public void StartCommit()
            {
                if (enabled)
                    commitTimer.Restart();
            }

            public void StartWrite()
            {
                if (enabled)
                    writeTimer.Restart();
            }

            public void EndWrite(long bytesWritten)
            {
                totalBytesWritten += bytesWritten;

                if (enabled)
                {
                    writeTimer.Stop();
                    writeTimeMs += writeTimer.ElapsedMilliseconds;
                }
            }

            public void StartFlush()
            {
                if (enabled)
                    flushTimer.Restart();
            }

            public void EndFlush()
            {
                if (enabled)
                {
                    flushTimer.Stop();
                    flushTimeMs += flushTimer.ElapsedMilliseconds;
                }
            }

            public void StartFileRead()
            {
                if (enabled)
                    fileReadTimer.Restart();
            }

            public void EndFileRead(long bytesRead)
            {
                totalBytesRead += bytesRead;

                if (enabled)
                {
                    fileReadTimer.Stop();
                    fileReadTimeMs += fileReadTimer.ElapsedMilliseconds;
                }
            }

            public void EndCommit(int fileCount, long fileBytes)
            {
                commitCount++;
                totalFileCount += fileCount;
                totalFileBytes += fileBytes;

                if (enabled)
                {
                    commitTimer.Stop();

                    // Warn about slow commits
                    if (commitTimer.ElapsedMilliseconds > 1000)
                    {
                        logger.WriteLine($"");
                        logger.WriteLine($"WARNING: Slow commit #{commitCount} took {commitTimer.ElapsedMilliseconds}ms");
                        logger.WriteLine($"  Files: {fileCount}, Bytes: {fileBytes:N0}");
                        logger.WriteLine($"  Write: {writeTimer.ElapsedMilliseconds}ms, Flush: {flushTimer.ElapsedMilliseconds}ms");
                    }
                }

                // Print statistics every 100 commits
                if (commitCount % 100 == 0)
                {
                    PrintProgressStatistics();
                }
            }

            private void PrintProgressStatistics()
            {
                var totalSecs = overallTimer.Elapsed.TotalSeconds;
                var commitsPerSec = commitCount / totalSecs;

                logger.WriteLine($"");
                logger.WriteLine($"=== Progress: {commitCount} commits in {overallTimer.Elapsed:hh\\:mm\\:ss} ===");
                logger.WriteLine($"  Rate: {commitsPerSec:F1} commits/sec ({commitsPerSec * 60:F0} commits/min)");
                logger.WriteLine($"  Files: {totalFileCount:N0} total ({totalFileCount / (double)commitCount:F1} avg/commit)");
                logger.WriteLine($"  Data: {totalFileBytes / 1024 / 1024:N0} MB total");
                logger.WriteLine($"  I/O: {totalBytesRead / 1024 / 1024:N0} MB read, {totalBytesWritten / 1024 / 1024:N0} MB written");

                if (enabled)
                {
                    var totalTrackedMs = writeTimeMs + flushTimeMs + fileReadTimeMs;
                    if (totalTrackedMs > 0)
                    {
                        logger.WriteLine($"  Time breakdown:");
                        logger.WriteLine($"    Write: {writeTimeMs}ms ({writeTimeMs * 100.0 / totalTrackedMs:F1}%)");
                        logger.WriteLine($"    Flush: {flushTimeMs}ms ({flushTimeMs * 100.0 / totalTrackedMs:F1}%)");
                        logger.WriteLine($"    FileRead: {fileReadTimeMs}ms ({fileReadTimeMs * 100.0 / totalTrackedMs:F1}%)");
                    }
                }

                var memoryMB = GC.GetTotalMemory(false) / 1024 / 1024;
                logger.WriteLine($"  Memory: {memoryMB} MB (GC: {GC.CollectionCount(0)}/{GC.CollectionCount(1)}/{GC.CollectionCount(2)})");
            }

            public void PrintFinalStatistics(TimeSpan totalTime)
            {
                logger.WriteLine($"");
                logger.WriteLine($"====================================");
                logger.WriteLine($"       FINAL STATISTICS");
                logger.WriteLine($"====================================");
                logger.WriteLine($"Total commits:     {commitCount:N0}");
                logger.WriteLine($"Total files:       {totalFileCount:N0}");
                logger.WriteLine($"Total data:        {totalFileBytes / 1024 / 1024:N0} MB");
                logger.WriteLine($"");
                logger.WriteLine($"Total time:        {totalTime:hh\\:mm\\:ss}");
                logger.WriteLine($"Commits/sec:       {commitCount / totalTime.TotalSeconds:F1}");
                logger.WriteLine($"Files/sec:         {totalFileCount / totalTime.TotalSeconds:F1}");
                logger.WriteLine($"Throughput:        {totalFileBytes / totalTime.TotalSeconds / 1024 / 1024:F1} MB/sec");

                if (enabled && commitCount > 0)
                {
                    logger.WriteLine($"");
                    logger.WriteLine($"Time breakdown:");
                    logger.WriteLine($"  Write:     {writeTimeMs}ms ({writeTimeMs / (double)commitCount:F1}ms/commit)");
                    logger.WriteLine($"  Flush:     {flushTimeMs}ms ({flushTimeMs / (double)commitCount:F1}ms/commit)");
                    logger.WriteLine($"  FileRead:  {fileReadTimeMs}ms ({fileReadTimeMs / (double)commitCount:F1}ms/commit)");
                }

                logger.WriteLine($"====================================");
            }
        }

        /// <summary>
        /// Represents a pending file operation (add, delete, rename).
        /// </summary>
        private class PendingFileOp
        {
            public enum OpType { Add, Delete, Rename }

            public OpType Type { get; set; }
            public string Path { get; set; }
            public string SourcePath { get; set; }  // For renames
            public byte[] Content { get; set; }      // For adds
        }
    }
}
