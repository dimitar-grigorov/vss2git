using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Hpdi.Vss2Git
{
    /// <summary>
    /// Implements IGitRepository using git fast-import for streaming bulk imports.
    /// Pipes commands to a long-running 'git fast-import' process. File data is sent
    /// inline with commit commands. The process is started on Init() and terminated
    /// with "done\n" on Dispose().
    /// </summary>
    class FastImportRepository : IGitRepository
    {
        private readonly string repoPath;
        private readonly string repoPathPrefix;
        private readonly Logger logger;
        private readonly PerformanceTracker perfTracker;
        private readonly Stopwatch stopwatch = new Stopwatch();

        private string gitExecutable;
        private Process fastImportProcess;
        private Stream stdin;
        private StringBuilder stderrBuffer;
        private Thread stderrThread;
        private Encoding commitEncoding = Encoding.UTF8;
        private string branchRef;

        private int nextMark = 1;
        private int lastCommitMark;

        // Pending operations buffered between Add/Remove/Move and Commit
        private readonly List<PendingModify> pendingModify = new List<PendingModify>();
        private readonly List<string> pendingDelete = new List<string>();
        private readonly List<(string source, string dest)> pendingRename =
            new List<(string, string)>();

        // Tree state: tracks SHA-256 hex hash of each file currently in the tree.
        // Used to skip re-adding files with unchanged content (matches Process/LibGit2Sharp
        // behavior where git detects same-tree commits and returns false).
        private readonly Dictionary<string, string> treeState =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private struct PendingModify
        {
            public string RelativePath;
            public byte[] Content;
        }

        public TimeSpan ElapsedTime => stopwatch.Elapsed;

        public Encoding CommitEncoding
        {
            get => commitEncoding;
            set => commitEncoding = value;
        }

        public FastImportRepository(string repoPath, Logger logger,
            PerformanceTracker perfTracker = null)
        {
            this.repoPath = repoPath;
            this.repoPathPrefix = repoPath.TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            this.logger = logger;
            this.perfTracker = perfTracker;
        }

        public void Init()
        {
            stopwatch.Start();
            try
            {
                using (perfTracker?.Start("Git:init"))
                {
                    FindGitExecutable();

                    // git init (required before fast-import)
                    RunGitCommand("init");

                    // Detect default branch (may be "main" or "master" depending on git config)
                    branchRef = RunGitCommandOutput("symbolic-ref HEAD").Trim();
                    if (string.IsNullOrEmpty(branchRef))
                        branchRef = "refs/heads/master";
                    logger.WriteLine("FastImport: target branch {0}", branchRef);

                    // Start git fast-import process
                    var psi = new ProcessStartInfo(gitExecutable, "fast-import --done --quiet")
                    {
                        WorkingDirectory = repoPath,
                        UseShellExecute = false,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    logger.WriteLine("FastImport: starting git fast-import");
                    fastImportProcess = Process.Start(psi);
                    stdin = fastImportProcess.StandardInput.BaseStream;

                    // Read stderr asynchronously to prevent blocking
                    stderrBuffer = new StringBuilder();
                    stderrThread = new Thread(() =>
                    {
                        try
                        {
                            var reader = fastImportProcess.StandardError;
                            string line;
                            while ((line = reader.ReadLine()) != null)
                            {
                                lock (stderrBuffer)
                                {
                                    if (stderrBuffer.Length > 0)
                                        stderrBuffer.AppendLine();
                                    stderrBuffer.Append(line);
                                }
                            }
                        }
                        catch { }
                    })
                    {
                        IsBackground = true,
                        Name = "FastImport-stderr"
                    };
                    stderrThread.Start();

                    // Also drain stdout in background (fast-import --quiet shouldn't
                    // produce output, but drain anyway to prevent pipe deadlock)
                    var stdoutThread = new Thread(() =>
                    {
                        try { fastImportProcess.StandardOutput.ReadToEnd(); }
                        catch { }
                    })
                    {
                        IsBackground = true,
                        Name = "FastImport-stdout"
                    };
                    stdoutThread.Start();

                    // Declare that we will send 'done' to terminate the stream
                    WriteCommand("feature done\n");
                }
            }
            finally
            {
                stopwatch.Stop();
            }
        }

        public void SetConfig(string name, string value)
        {
            stopwatch.Start();
            try
            {
                using (perfTracker?.Start("Git:config"))
                {
                    // Skip i18n.commitencoding for non-UTF-8 (same as LibGit2Sharp backend).
                    // fast-import always writes UTF-8 commit messages; setting a different
                    // encoding in git config would confuse git log.
                    if (name == "i18n.commitencoding" &&
                        !value.Equals("utf-8", StringComparison.OrdinalIgnoreCase))
                    {
                        logger.WriteLine("FastImport: skipping config {0} = {1} (always UTF-8)",
                            name, value);
                        return;
                    }

                    RunGitCommand("config " + name + " \"" + value + "\"");
                }
            }
            finally
            {
                stopwatch.Stop();
            }
        }

        public bool Add(string path)
        {
            stopwatch.Start();
            try
            {
                using (perfTracker?.Start("Git:add"))
                {
                    return AddFile(path);
                }
            }
            finally
            {
                stopwatch.Stop();
            }
        }

        public bool Add(IEnumerable<string> paths)
        {
            if (CollectionUtil.IsEmpty(paths))
                return false;

            stopwatch.Start();
            try
            {
                using (perfTracker?.Start("Git:add"))
                {
                    bool anyAdded = false;
                    foreach (var path in paths)
                    {
                        anyAdded |= AddFile(path);
                    }
                    return anyAdded;
                }
            }
            finally
            {
                stopwatch.Stop();
            }
        }

        public bool AddAll()
        {
            // GitExporter always calls AddAll(changedPaths) from CommitChangeset().
            // This no-arg overload is a fallback that shouldn't normally be reached.
            logger.WriteLine("WARNING: FastImport.AddAll() without paths — no files staged");
            return true;
        }

        public bool AddAll(IEnumerable<string> changedPaths)
        {
            if (CollectionUtil.IsEmpty(changedPaths))
                return AddAll();

            stopwatch.Start();
            try
            {
                using (perfTracker?.Start("Git:addAll"))
                {
                    foreach (var path in changedPaths)
                    {
                        var relativePath = ToRelativePath(path);
                        if (File.Exists(path))
                        {
                            var content = File.ReadAllBytes(path);
                            var hash = ComputeHash(content);

                            // Skip if content is identical to what's already in the tree
                            if (treeState.TryGetValue(relativePath, out var existing) &&
                                existing == hash)
                            {
                                continue;
                            }

                            pendingModify.Add(new PendingModify
                            {
                                RelativePath = relativePath,
                                Content = content
                            });
                        }
                        else
                        {
                            // File was deleted from disk — emit D command
                            pendingDelete.Add(relativePath);
                        }
                    }
                    return true;
                }
            }
            finally
            {
                stopwatch.Stop();
            }
        }

        public void Remove(string path, bool recursive)
        {
            stopwatch.Start();
            try
            {
                using (perfTracker?.Start("Git:remove"))
                {
                    var relativePath = ToRelativePath(path);

                    // Buffer D command. fast-import's D handles both files and
                    // directories (deletes entire subtree for directory paths).
                    pendingDelete.Add(relativePath);

                    // Purge any pending modifications under the deleted path so
                    // the D→R→M emission order doesn't re-create them.
                    if (recursive)
                    {
                        var prefix = relativePath.TrimEnd('/') + "/";
                        pendingModify.RemoveAll(m =>
                            m.RelativePath.Equals(relativePath, StringComparison.OrdinalIgnoreCase) ||
                            m.RelativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
                    }
                    else
                    {
                        pendingModify.RemoveAll(m =>
                            m.RelativePath.Equals(relativePath, StringComparison.OrdinalIgnoreCase));
                    }

                    // Delete from filesystem (matches LibGit2Sharp behavior —
                    // GitExporter expects the filesystem to reflect the state)
                    if (recursive && Directory.Exists(path))
                    {
                        Directory.Delete(path, true);
                    }
                    else if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
            }
            finally
            {
                stopwatch.Stop();
            }
        }

        public void Move(string sourcePath, string destPath)
        {
            stopwatch.Start();
            try
            {
                using (perfTracker?.Start("Git:move"))
                {
                    var relSource = ToRelativePath(sourcePath);
                    var relDest = ToRelativePath(destPath);

                    // Fast-import's R command only works on paths already in the
                    // committed tree. For uncommitted paths (still in pendingModify),
                    // update the pending modify's path directly.
                    bool handled = RewritePendingPaths(relSource, relDest);

                    // If source is in committed tree, emit R command for fast-import
                    if (treeState.ContainsKey(relSource) ||
                        HasTreeStatePrefix(relSource + "/"))
                    {
                        pendingRename.Add((relSource, relDest));
                    }
                    else if (!handled)
                    {
                        // Fallback: emit R anyway (source may be from a prior
                        // rename in the same changeset)
                        pendingRename.Add((relSource, relDest));
                    }

                    // Perform filesystem move (GitExporter reads from new path)
                    bool isDirectory = Directory.Exists(sourcePath);
                    bool isFile = !isDirectory && File.Exists(sourcePath);

                    if (isFile)
                    {
                        var destDir = Path.GetDirectoryName(destPath);
                        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                            Directory.CreateDirectory(destDir);
                        File.Move(sourcePath, destPath);
                    }
                    else if (isDirectory)
                    {
                        var destDir = Path.GetDirectoryName(destPath);
                        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                            Directory.CreateDirectory(destDir);
                        Directory.Move(sourcePath, destPath);
                    }
                }
            }
            finally
            {
                stopwatch.Stop();
            }
        }

        public bool Commit(string authorName, string authorEmail,
            string comment, DateTime localTime)
        {
            stopwatch.Start();
            try
            {
                using (perfTracker?.Start("Git:commit"))
                {
                    if (pendingModify.Count == 0 && pendingDelete.Count == 0 &&
                        pendingRename.Count == 0)
                    {
                        return false;
                    }

                    var commitMark = nextMark++;
                    var timestamp = FormatTimestamp(localTime);
                    var message = LibGit2SharpRepository.CleanupMessage(comment);
                    var messageBytes = Encoding.UTF8.GetBytes(message);

                    // commit header
                    var sb = new StringBuilder();
                    sb.Append("commit ").Append(branchRef).Append('\n');
                    sb.Append("mark :").Append(commitMark).Append('\n');
                    sb.Append("author ").Append(authorName)
                      .Append(" <").Append(authorEmail).Append("> ")
                      .Append(timestamp).Append('\n');
                    sb.Append("committer ").Append(authorName)
                      .Append(" <").Append(authorEmail).Append("> ")
                      .Append(timestamp).Append('\n');

                    WriteCommand(sb.ToString());
                    WriteDataBlock(messageBytes);

                    // Parent reference (omit for first commit — creates root commit)
                    if (lastCommitMark > 0)
                    {
                        WriteCommand("from :" + lastCommitMark + "\n");
                    }

                    // Emit deletions first, then renames, then modifications.
                    // D before R ensures stale destination entries are cleared
                    // before a rename overwrites them (e.g. MoveFrom cleanup).
                    foreach (var path in pendingDelete)
                    {
                        WriteCommand("D " + QuotePath(path) + "\n");
                    }

                    foreach (var (source, dest) in pendingRename)
                    {
                        WriteCommand("R " + QuotePath(source) + " " + QuotePath(dest) + "\n");
                    }

                    foreach (var mod in pendingModify)
                    {
                        WriteCommand("M 100644 inline " + QuotePath(mod.RelativePath) + "\n");
                        WriteDataBlock(mod.Content);
                    }

                    // Trailing LF to end the commit
                    WriteCommand("\n");
                    stdin.Flush();

                    lastCommitMark = commitMark;

                    // Update tree state to reflect the committed changes (D→R→M order)
                    foreach (var path in pendingDelete)
                    {
                        // Could be a directory delete — remove all matching prefixes
                        var prefix = path.TrimEnd('/') + "/";
                        var toRemove = new List<string>();
                        foreach (var key in treeState.Keys)
                        {
                            if (key.Equals(path, StringComparison.OrdinalIgnoreCase) ||
                                key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                            {
                                toRemove.Add(key);
                            }
                        }
                        foreach (var key in toRemove)
                            treeState.Remove(key);
                    }
                    foreach (var (source, dest) in pendingRename)
                    {
                        // For directory renames, move all matching prefixes
                        var prefix = source.TrimEnd('/') + "/";
                        var toMove = new List<(string oldKey, string hash)>();
                        foreach (var kvp in treeState)
                        {
                            if (kvp.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                            {
                                toMove.Add((kvp.Key, kvp.Value));
                            }
                        }
                        if (toMove.Count > 0)
                        {
                            var destPrefix = dest.TrimEnd('/') + "/";
                            foreach (var (oldKey, hash) in toMove)
                            {
                                treeState.Remove(oldKey);
                                treeState[destPrefix + oldKey.Substring(prefix.Length)] = hash;
                            }
                        }
                        else if (treeState.TryGetValue(source, out var fileHash))
                        {
                            // Single file rename
                            treeState.Remove(source);
                            treeState[dest] = fileHash;
                        }
                    }
                    foreach (var mod in pendingModify)
                    {
                        treeState[mod.RelativePath] = ComputeHash(mod.Content);
                    }

                    ClearPendingOperations();

                    return true;
                }
            }
            finally
            {
                stopwatch.Stop();
            }
        }

        public void Tag(string name, string taggerName, string taggerEmail,
            string comment, DateTime localTime)
        {
            stopwatch.Start();
            try
            {
                using (perfTracker?.Start("Git:tag"))
                {
                    if (lastCommitMark == 0)
                    {
                        logger.WriteLine("WARNING: FastImport: cannot create tag '{0}' before first commit",
                            name);
                        return;
                    }

                    var timestamp = FormatTimestamp(localTime);
                    var message = LibGit2SharpRepository.CleanupMessage(comment);
                    if (string.IsNullOrEmpty(message))
                        message = name + "\n";
                    var messageBytes = Encoding.UTF8.GetBytes(message);

                    var sb = new StringBuilder();
                    sb.Append("tag ").Append(name).Append('\n');
                    sb.Append("from :").Append(lastCommitMark).Append('\n');
                    sb.Append("tagger ").Append(taggerName)
                      .Append(" <").Append(taggerEmail).Append("> ")
                      .Append(timestamp).Append('\n');

                    WriteCommand(sb.ToString());
                    WriteDataBlock(messageBytes);
                    // No extra \n here — tag has no commit terminator;
                    // WriteDataBlock's trailing LF is sufficient.
                    stdin.Flush();
                }
            }
            finally
            {
                stopwatch.Stop();
            }
        }

        public void Compact()
        {
            // Fast-import packs objects internally. Emit checkpoint to flush
            // the current packfile to disk — provides a recovery point.
            stopwatch.Start();
            try
            {
                using (perfTracker?.Start("Git:compact"))
                {
                    logger.WriteLine("FastImport: checkpoint");
                    WriteCommand("checkpoint\n");
                    stdin.Flush();
                }
            }
            finally
            {
                stopwatch.Stop();
            }
        }

        public void Dispose()
        {
            if (fastImportProcess != null)
            {
                try
                {
                    if (!fastImportProcess.HasExited)
                    {
                        // Signal end of stream
                        WriteCommand("done\n");
                        stdin.Flush();
                        stdin.Close();

                        // Wait for process to finish
                        if (!fastImportProcess.WaitForExit(120_000))
                        {
                            logger.WriteLine("WARNING: git fast-import timed out after 120s, killing");
                            fastImportProcess.Kill();
                        }
                    }

                    // Wait for stderr thread to finish collecting output
                    stderrThread?.Join(5000);

                    var exitCode = fastImportProcess.ExitCode;
                    if (exitCode != 0)
                    {
                        var stderr = GetStderrOutput();
                        logger.WriteLine("WARNING: git fast-import exited with code {0}: {1}",
                            exitCode, stderr);
                    }
                    else
                    {
                        logger.WriteLine("FastImport: completed successfully");
                    }
                }
                catch (Exception ex)
                {
                    logger.WriteLine("WARNING: error closing fast-import: {0}", ex.Message);
                }
                finally
                {
                    fastImportProcess.Dispose();
                    fastImportProcess = null;
                }
            }
        }

        #region Private helpers

        private void WriteCommand(string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            stdin.Write(bytes, 0, bytes.Length);
        }

        private void WriteDataBlock(byte[] data)
        {
            WriteCommand("data " + data.Length + "\n");
            if (data.Length > 0)
            {
                stdin.Write(data, 0, data.Length);
            }
            WriteCommand("\n");
        }

        private static string FormatTimestamp(DateTime localTime)
        {
            var utcTime = TimeZoneInfo.ConvertTimeToUtc(localTime);
            var epoch = (long)(new DateTimeOffset(utcTime, TimeSpan.Zero) -
                DateTimeOffset.UnixEpoch).TotalSeconds;
            return epoch + " +0000";
        }

        /// <summary>
        /// Quotes a path for fast-import if it contains special characters.
        /// Fast-import requires C-style quoting for paths with SP, LF, DQ, or backslash.
        /// </summary>
        internal static string QuotePath(string path)
        {
            if (path.IndexOfAny(new[] { ' ', '"', '\\', '\n' }) >= 0)
            {
                return "\"" + path
                    .Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\n", "\\n") + "\"";
            }
            return path;
        }

        private string ToRelativePath(string absolutePath)
        {
            if (absolutePath.StartsWith(repoPathPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return absolutePath.Substring(repoPathPrefix.Length)
                    .Replace('\\', '/');
            }
            return absolutePath.Replace('\\', '/');
        }

        private bool AddFile(string path)
        {
            if (!File.Exists(path))
                return false;

            var relativePath = ToRelativePath(path);
            var content = File.ReadAllBytes(path);
            var hash = ComputeHash(content);

            // Skip if content is identical to what's already in the tree
            if (treeState.TryGetValue(relativePath, out var existing) &&
                existing == hash)
            {
                return false;
            }

            pendingModify.Add(new PendingModify
            {
                RelativePath = relativePath,
                Content = content
            });
            return true;
        }

        /// <summary>
        /// Rewrites pending modify paths when source is renamed before commit.
        /// Returns true if any pending paths were updated.
        /// </summary>
        private bool RewritePendingPaths(string relSource, string relDest)
        {
            bool updated = false;

            // Single file match
            for (int i = 0; i < pendingModify.Count; i++)
            {
                if (pendingModify[i].RelativePath.Equals(relSource,
                    StringComparison.OrdinalIgnoreCase))
                {
                    pendingModify[i] = new PendingModify
                    {
                        RelativePath = relDest,
                        Content = pendingModify[i].Content
                    };
                    updated = true;
                }
            }

            if (!updated)
            {
                // Directory prefix match — rename all matching pending modifies
                var prefix = relSource.TrimEnd('/') + "/";
                var destPrefix = relDest.TrimEnd('/') + "/";
                for (int i = 0; i < pendingModify.Count; i++)
                {
                    if (pendingModify[i].RelativePath.StartsWith(prefix,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        pendingModify[i] = new PendingModify
                        {
                            RelativePath = destPrefix +
                                pendingModify[i].RelativePath.Substring(prefix.Length),
                            Content = pendingModify[i].Content
                        };
                        updated = true;
                    }
                }
            }

            return updated;
        }

        private bool HasTreeStatePrefix(string prefix)
        {
            foreach (var key in treeState.Keys)
            {
                if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static string ComputeHash(byte[] content)
        {
            var hash = SHA256.HashData(content);
            return Convert.ToHexString(hash);
        }

        public IList<string> FinalizeRepository()
        {
            stopwatch.Start();
            try
            {
                // git fast-import doesn't create the index - we must do it explicitly
                logger.WriteLine("Creating git index from HEAD");
                RunGitCommand("reset HEAD");

                // Check for files in HEAD that are missing from working tree
                var deletedFiles = new List<string>();
                var output = RunGitCommandOutput("diff --name-only HEAD");
                if (!string.IsNullOrWhiteSpace(output))
                {
                    foreach (var line in output.Split('\n'))
                    {
                        var file = line.Trim();
                        if (!string.IsNullOrEmpty(file))
                        {
                            deletedFiles.Add(file);
                        }
                    }
                }

                return deletedFiles;
            }
            finally
            {
                stopwatch.Stop();
            }
        }

        private void ClearPendingOperations()
        {
            pendingModify.Clear();
            pendingDelete.Clear();
            pendingRename.Clear();
        }

        private string GetStderrOutput()
        {
            lock (stderrBuffer)
            {
                return stderrBuffer.ToString();
            }
        }

        private void FindGitExecutable()
        {
            string path = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(path))
            {
                foreach (string dir in path.Split(Path.PathSeparator))
                {
                    var candidate = Path.Combine(dir, "git.exe");
                    if (File.Exists(candidate))
                    {
                        gitExecutable = candidate;
                        return;
                    }
                    candidate = Path.Combine(dir, "git");
                    if (File.Exists(candidate))
                    {
                        gitExecutable = candidate;
                        return;
                    }
                }
            }
            throw new FileNotFoundException(
                "Git executable not found in PATH. Please ensure git.exe or git is available.");
        }

        private void RunGitCommand(string args)
        {
            var output = RunGitCommandOutput(args);
        }

        private string RunGitCommandOutput(string args)
        {
            var psi = new ProcessStartInfo(gitExecutable, args)
            {
                WorkingDirectory = repoPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            var stdout = proc.StandardOutput.ReadToEnd();
            var stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit();
            if (proc.ExitCode != 0)
            {
                throw new ProcessExitException(
                    string.Format("git {0} failed (exit code {1})", args, proc.ExitCode),
                    gitExecutable, args, stdout, stderr);
            }
            return stdout;
        }

        #endregion
    }
}
