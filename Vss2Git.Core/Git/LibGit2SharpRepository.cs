using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using LibGit2Sharp;

namespace Hpdi.Vss2Git
{
    /// <summary>
    /// Implements IGitRepository using the LibGit2Sharp managed library.
    /// Eliminates git.exe process spawning overhead.
    /// </summary>
    class LibGit2SharpRepository : IGitRepository
    {
        private readonly string repoPath;
        private readonly string repoPathPrefix;
        private readonly Logger logger;
        private readonly PerformanceTracker perfTracker;
        private readonly Stopwatch stopwatch = new Stopwatch();
        private Repository repo;
        private Encoding commitEncoding = Encoding.UTF8;

        public TimeSpan ElapsedTime => stopwatch.Elapsed;

        public Encoding CommitEncoding
        {
            get => commitEncoding;
            set => commitEncoding = value;
        }

        public LibGit2SharpRepository(string repoPath, Logger logger,
            PerformanceTracker perfTracker = null)
        {
            this.repoPath = repoPath;
            // Precompute prefix for path conversion (with trailing separator)
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
                    logger.WriteLine("LibGit2Sharp: init {0}", repoPath);
                    Repository.Init(repoPath);
                    repo = new Repository(repoPath);
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
                    logger.WriteLine("LibGit2Sharp: config {0} = {1}", name, value);
                    repo.Config.Set(name, value, ConfigurationLevel.Local);
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
                    var relativePath = ToRelativePath(path);

                    // Directories without files are no-ops for git
                    var fullPath = Path.Combine(repoPath, relativePath);
                    if (Directory.Exists(fullPath))
                    {
                        if (!Directory.EnumerateFiles(fullPath, "*", SearchOption.AllDirectories).Any())
                            return false;
                    }
                    else if (!File.Exists(fullPath))
                    {
                        return false;
                    }

                    Commands.Stage(repo, relativePath);
                    return true;
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
                    foreach (var path in paths)
                    {
                        Commands.Stage(repo, ToRelativePath(path));
                    }
                    return true;
                }
            }
            finally
            {
                stopwatch.Stop();
            }
        }

        public bool AddAll()
        {
            stopwatch.Start();
            try
            {
                using (perfTracker?.Start("Git:addAll"))
                {
                    Commands.Stage(repo, "*");
                    return true;
                }
            }
            finally
            {
                stopwatch.Stop();
            }
        }

        public bool AddAll(IEnumerable<string> changedPaths)
        {
            return AddAll();
        }

        public void Remove(string path, bool recursive)
        {
            stopwatch.Start();
            try
            {
                using (perfTracker?.Start("Git:remove"))
                {
                    var relativePath = ToRelativePath(path);
                    logger.WriteLine("LibGit2Sharp: remove {0}{1}",
                        recursive ? "-rf " : "", relativePath);

                    if (recursive && Directory.Exists(path))
                    {
                        // Remove all indexed entries under this directory
                        var prefix = relativePath.Replace('\\', '/');
                        if (!prefix.EndsWith("/")) prefix += "/";

                        var entries = repo.Index
                            .Where(e => e.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                            .Select(e => e.Path)
                            .ToList();

                        foreach (var entry in entries)
                        {
                            repo.Index.Remove(entry);
                        }
                        repo.Index.Write();

                        // Also remove from working directory (matches git rm -rf)
                        if (Directory.Exists(path))
                        {
                            Directory.Delete(path, true);
                        }
                    }
                    else
                    {
                        Commands.Remove(repo, relativePath, removeFromWorkingDirectory: true);
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
                    logger.WriteLine("LibGit2Sharp: move {0} -> {1}", relSource, relDest);

                    // git mv = filesystem move + index update.
                    // CaseSensitiveRename in GitExporter passes git.Move as delegate,
                    // so we must handle the filesystem move ourselves.

                    bool isDirectory = Directory.Exists(sourcePath);
                    bool isFile = !isDirectory && File.Exists(sourcePath);

                    if (isFile)
                    {
                        var destDir = Path.GetDirectoryName(destPath);
                        if (!Directory.Exists(destDir))
                            Directory.CreateDirectory(destDir);
                        File.Move(sourcePath, destPath);

                        // Update index: remove old, add new
                        repo.Index.Remove(relSource.Replace('\\', '/'));
                        repo.Index.Add(relDest.Replace('\\', '/'));
                        repo.Index.Write();
                    }
                    else if (isDirectory)
                    {
                        // Collect old index entries before moving
                        var prefix = relSource.Replace('\\', '/');
                        if (!prefix.EndsWith("/")) prefix += "/";

                        var oldEntries = repo.Index
                            .Where(e => e.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                            .Select(e => e.Path)
                            .ToList();

                        // Filesystem move
                        Directory.Move(sourcePath, destPath);

                        // Remove old entries from index
                        foreach (var entry in oldEntries)
                        {
                            repo.Index.Remove(entry);
                        }

                        // Add new entries
                        var newPrefix = relDest.Replace('\\', '/');
                        if (!newPrefix.EndsWith("/")) newPrefix += "/";
                        foreach (var oldEntry in oldEntries)
                        {
                            var newEntry = newPrefix + oldEntry.Substring(prefix.Length);
                            repo.Index.Add(newEntry);
                        }
                        repo.Index.Write();
                    }
                }
            }
            finally
            {
                stopwatch.Stop();
            }
        }

        public bool Commit(string authorName, string authorEmail, string comment, DateTime localTime)
        {
            stopwatch.Start();
            try
            {
                using (perfTracker?.Start("Git:commit"))
                {
                    // Check if there's anything to commit (matches "nothing to commit" behavior)
                    var status = repo.RetrieveStatus();
                    if (!status.IsDirty)
                    {
                        return false;
                    }

                    // Match GitWrapper: convert to UTC with +0000 offset
                    var utcTime = TimeZoneInfo.ConvertTimeToUtc(localTime);
                    var dateTimeOffset = new DateTimeOffset(utcTime, TimeSpan.Zero);

                    var author = new Signature(authorName, authorEmail, dateTimeOffset);
                    var committer = author; // Same person, matching GitWrapper behavior

                    repo.Commit(comment ?? "", author, committer);
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
                    logger.WriteLine("LibGit2Sharp: tag {0}", name);

                    var utcTime = TimeZoneInfo.ConvertTimeToUtc(localTime);
                    var dateTimeOffset = new DateTimeOffset(utcTime, TimeSpan.Zero);
                    var tagger = new Signature(taggerName, taggerEmail, dateTimeOffset);

                    if (string.IsNullOrEmpty(comment))
                    {
                        // Annotated tag with empty message (matches git tag -m "")
                        repo.Tags.Add(name, repo.Head.Tip, tagger, "");
                    }
                    else
                    {
                        repo.Tags.Add(name, repo.Head.Tip, tagger, comment);
                    }
                }
            }
            finally
            {
                stopwatch.Stop();
            }
        }

        public void Dispose()
        {
            repo?.Dispose();
            repo = null;
        }

        /// <summary>
        /// Converts an absolute path to a path relative to the repository root,
        /// using forward slashes (git convention).
        /// </summary>
        private string ToRelativePath(string absolutePath)
        {
            if (absolutePath.StartsWith(repoPathPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return absolutePath.Substring(repoPathPrefix.Length).Replace('\\', '/');
            }
            return absolutePath.Replace('\\', '/');
        }
    }
}
