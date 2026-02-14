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
    /// Uses TreeDefinition for O(k) commits (k = changed files per commit),
    /// bypassing the git index to avoid O(n) full-tree scans.
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
        private TreeDefinition currentTree;

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
                    currentTree = new TreeDefinition();
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
            // Not used in current pipeline - AddAll(changedPaths) handles staging
            stopwatch.Start();
            try
            {
                using (perfTracker?.Start("Git:add"))
                {
                    if (!File.Exists(path))
                        return false;

                    var relativePath = ToRelativePath(path);
                    var blob = repo.ObjectDatabase.CreateBlob(path);
                    currentTree.Add(relativePath, blob, Mode.NonExecutableFile);
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
                        if (File.Exists(path))
                        {
                            var relativePath = ToRelativePath(path);
                            var blob = repo.ObjectDatabase.CreateBlob(path);
                            currentTree.Add(relativePath, blob, Mode.NonExecutableFile);
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

        public bool AddAll()
        {
            // Fallback: sync TreeDefinition from working tree via index
            stopwatch.Start();
            try
            {
                using (perfTracker?.Start("Git:addAll"))
                {
                    Commands.Stage(repo, "*");
                    var tree = repo.ObjectDatabase.CreateTree(repo.Index);
                    currentTree = TreeDefinition.From(tree);
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
            if (changedPaths == null)
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
                            var blob = repo.ObjectDatabase.CreateBlob(path);
                            currentTree.Add(relativePath, blob, Mode.NonExecutableFile);
                        }
                        else
                        {
                            currentTree.Remove(relativePath);
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
                    logger.WriteLine("LibGit2Sharp: remove {0}{1}",
                        recursive ? "-rf " : "", relativePath);

                    // Materialize before recursive remove: TreeDefinition.Remove("dir")
                    // silently fails on uncommitted subtrees (L1 bug).
                    if (recursive)
                    {
                        var tempTree = repo.ObjectDatabase.CreateTree(currentTree);
                        currentTree = TreeDefinition.From(tempTree);
                    }
                    currentTree.Remove(relativePath);

                    // Remove from working directory
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
                    logger.WriteLine("LibGit2Sharp: move {0} -> {1}", relSource, relDest);

                    bool isDirectory = Directory.Exists(sourcePath);
                    bool isFile = !isDirectory && File.Exists(sourcePath);

                    if (isFile)
                    {
                        var destDir = Path.GetDirectoryName(destPath);
                        if (!Directory.Exists(destDir))
                            Directory.CreateDirectory(destDir);
                        File.Move(sourcePath, destPath);

                        // Update tree: move entry from old path to new path
                        var entry = currentTree[relSource];
                        if (entry != null)
                        {
                            currentTree.Add(relDest, entry);
                            currentTree.Remove(relSource);
                        }
                        else
                        {
                            // Entry not in tree yet - create blob from new location
                            var blob = repo.ObjectDatabase.CreateBlob(destPath);
                            currentTree.Add(relDest, blob, Mode.NonExecutableFile);
                        }
                    }
                    else if (isDirectory)
                    {
                        // Materialize before move: Add(dest, tree[source]) crashes
                        // on uncommitted subtrees (L2 bug).
                        var tempTree = repo.ObjectDatabase.CreateTree(currentTree);
                        currentTree = TreeDefinition.From(tempTree);

                        var dirEntry = currentTree[relSource];

                        // Filesystem move
                        Directory.Move(sourcePath, destPath);

                        if (dirEntry != null)
                        {
                            currentTree.Add(relDest, dirEntry);
                            currentTree.Remove(relSource);
                        }
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
                    // Build tree from our incrementally-maintained TreeDefinition
                    var tree = repo.ObjectDatabase.CreateTree(currentTree);

                    // Check if tree is same as HEAD (nothing changed)
                    if (repo.Head.Tip != null && tree.Id == repo.Head.Tip.Tree.Id)
                    {
                        return false;
                    }

                    // Match GitWrapper: convert to UTC with +0000 offset
                    var utcTime = TimeZoneInfo.ConvertTimeToUtc(localTime);
                    var dateTimeOffset = new DateTimeOffset(utcTime, TimeSpan.Zero);

                    var author = new Signature(authorName, authorEmail, dateTimeOffset);
                    var committer = author;

                    var parents = repo.Head.Tip != null
                        ? new[] { repo.Head.Tip }
                        : Array.Empty<Commit>();

                    var commit = repo.ObjectDatabase.CreateCommit(
                        author, committer, CleanupMessage(comment), tree, parents, false);

                    // Update HEAD to point to the new commit
                    repo.Refs.UpdateTarget(repo.Refs.Head, commit.Id);

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

                    repo.Tags.Add(name, repo.Head.Tip, tagger, CleanupMessage(comment));
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
        /// Normalizes a commit/tag message to match git's default 'strip' cleanup:
        /// CRLF→LF, strip trailing whitespace per line, strip leading/trailing
        /// blank lines, ensure trailing newline.
        /// </summary>
        internal static string CleanupMessage(string comment)
        {
            if (string.IsNullOrEmpty(comment))
                return "";

            var lines = comment.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

            // Strip trailing whitespace from each line
            for (int i = 0; i < lines.Length; i++)
                lines[i] = lines[i].TrimEnd();

            // Strip leading blank lines
            int start = 0;
            while (start < lines.Length && lines[start].Length == 0)
                start++;

            // Strip trailing blank lines
            int end = lines.Length - 1;
            while (end >= start && lines[end].Length == 0)
                end--;

            if (start > end)
                return "";

            var sb = new StringBuilder();
            for (int i = start; i <= end; i++)
            {
                sb.Append(lines[i]);
                sb.Append('\n');
            }
            return sb.ToString();
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
