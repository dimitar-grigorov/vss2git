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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Hpdi.Vss2Git
{
    /// <summary>
    /// Wraps execution of Git and implements the common Git commands.
    /// </summary>
    /// <author>Trevor Robinson</author>
    class GitWrapper : IGitRepository, IDisposable
    {
        private readonly string repoPath;
        private readonly Logger logger;
        private readonly PerformanceTracker perfTracker;
        private readonly Stopwatch stopwatch = new Stopwatch();
        private string gitExecutable = "git.exe";
        private string gitInitialArguments = null;
        private bool shellQuoting = false;
        private Encoding commitEncoding = Encoding.UTF8;

        public TimeSpan ElapsedTime
        {
            get { return stopwatch.Elapsed; }
        }

        public string GitExecutable
        {
            get { return gitExecutable; }
            set { gitExecutable = value; }
        }

        public string GitInitialArguments
        {
            get { return gitInitialArguments; }
            set { gitInitialArguments = value; }
        }

        public bool ShellQuoting
        {
            get { return shellQuoting; }
            set { shellQuoting = value; }
        }

        public Encoding CommitEncoding
        {
            get { return commitEncoding; }
            set { commitEncoding = value; }
        }

        public GitWrapper(string repoPath, Logger logger, PerformanceTracker perfTracker = null)
        {
            this.repoPath = repoPath;
            this.logger = logger;
            this.perfTracker = perfTracker;
        }

        private bool FindExecutable()
        {
            string foundPath;
            if (FindInPathVar(GitExecutable, out foundPath))
            {
                gitExecutable = foundPath;
                gitInitialArguments = null;
                shellQuoting = false;
                return true;
            }
            if (FindInPathVar("git.cmd", out foundPath))
            {
                gitExecutable = "cmd.exe";
                gitInitialArguments = "/c git";
                shellQuoting = true;
                return true;
            }
            return false;
        }

        public void Init()
        {
            if (!FindExecutable())
            {
                throw new FileNotFoundException("Git executable not found in PATH. Please ensure git.exe or git.cmd is available.");
            }
            using (perfTracker?.Start("Git:init")) GitExec("init");
        }

        public void SetConfig(string name, string value)
        {
            using (perfTracker?.Start("Git:config")) GitExec("config " + name + " " + Quote(value));
        }

        public bool Add(string path)
        {
            var startInfo = GetStartInfo("add -- " + Quote(path));

            // add fails if there are no files (directories don't count)
            using (perfTracker?.Start("Git:add"))
                return ExecuteUnless(startInfo, "did not match any files");
        }

        public bool Add(IEnumerable<string> paths)
        {
            if (CollectionUtil.IsEmpty(paths))
            {
                return false;
            }

            var args = new StringBuilder("add -- ");
            CollectionUtil.Join(args, " ", CollectionUtil.Transform<string, string>(paths, Quote));
            var startInfo = GetStartInfo(args.ToString());

            // add fails if there are no files (directories don't count)
            using (perfTracker?.Start("Git:add"))
                return ExecuteUnless(startInfo, "did not match any files");
        }

        public bool AddAll()
        {
            var startInfo = GetStartInfo("add -A");

            // add fails if there are no files (directories don't count)
            using (perfTracker?.Start("Git:addAll"))
                return ExecuteUnless(startInfo, "did not match any files");
        }

        public bool AddAll(IEnumerable<string> changedPaths)
        {
            return AddAll();
        }

        public void Remove(string path, bool recursive)
        {
            using (perfTracker?.Start("Git:remove")) GitExec("rm " + (recursive ? "-rf " : "") + "-- " + Quote(path));
        }

        public void Move(string sourcePath, string destPath)
        {
            using (perfTracker?.Start("Git:move"))
            {
                var mvArgs = "mv -- " + Quote(sourcePath) + " " + Quote(destPath);
                var startInfo = GetStartInfo(mvArgs);
                if (!ExecuteUnless(startInfo, "bad source"))
                {
                    // git mv failed (untracked files in directory) - fall back to
                    // filesystem move; the next git add -A will pick up changes
                    logger.WriteLine("NOTE: git mv failed with 'bad source', falling back to filesystem move: {0} -> {1}",
                        sourcePath, destPath);
                    MoveFileSystem(sourcePath, destPath);
                }
            }
        }

        internal static void MoveFileSystem(string sourcePath, string destPath)
        {
            var destDir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);

            if (Directory.Exists(sourcePath))
                Directory.Move(sourcePath, destPath);
            else if (File.Exists(sourcePath))
                File.Move(sourcePath, destPath);
        }

        class TempFile : IDisposable
        {
            private readonly string name;
            private readonly FileStream fileStream;

            public string Name
            {
                get { return name; }
            }

            public TempFile()
            {
                name = Path.GetTempFileName();
                fileStream = new FileStream(name, FileMode.Truncate, FileAccess.Write, FileShare.Read);
            }

            public void Write(string text, Encoding encoding)
            {
                var bytes = encoding.GetBytes(text);
                fileStream.Write(bytes, 0, bytes.Length);
                fileStream.Flush();
            }

            public void Dispose()
            {
                if (fileStream != null)
                {
                    fileStream.Dispose();
                }
                if (name != null)
                {
                    File.Delete(name);
                }
            }
        }

        private void AddComment(string comment, ref string args, out TempFile tempFile)
        {
            tempFile = null;
            if (string.IsNullOrEmpty(comment))
            {
                args += " --allow-empty-message --no-edit -m \"\"";
            }
            else
            {
                // Need to use a temporary file to specify the comment when
                // not using default code page or contains newlines or contains non-ASCII characters.
                if (commitEncoding.CodePage != Encoding.Default.CodePage ||
                    comment.Contains('\n') ||
                    comment.Any(c => c > 127))
                {
                        logger.WriteLine("Generating temp file for comment: {0}", comment);
                        tempFile = new TempFile();
                        tempFile.Write(comment, commitEncoding);

                    // temporary path might contain spaces (e.g. "Documents and Settings")
                    args += " -F " + Quote(tempFile.Name);
                }
                else
                {
                    args += " -m " + Quote(comment);
                }
            }
        }

        public bool Commit(string authorName, string authorEmail, string comment, DateTime localTime)
        {
            TempFile commentFile;

            var args = "commit";
            AddComment(comment, ref args, out commentFile);

            using (commentFile)
            {
                var startInfo = GetStartInfo(args);
                startInfo.EnvironmentVariables["GIT_AUTHOR_NAME"] = authorName;
                startInfo.EnvironmentVariables["GIT_AUTHOR_EMAIL"] = authorEmail;
                startInfo.EnvironmentVariables["GIT_AUTHOR_DATE"] = GetUtcTimeString(localTime);

                // also setting the committer is supposedly useful for converting to Mercurial
                startInfo.EnvironmentVariables["GIT_COMMITTER_NAME"] = authorName;
                startInfo.EnvironmentVariables["GIT_COMMITTER_EMAIL"] = authorEmail;
                startInfo.EnvironmentVariables["GIT_COMMITTER_DATE"] = GetUtcTimeString(localTime);

                // ignore empty commits, since they are non-trivial to detect
                // (e.g. when renaming a directory)
                using (perfTracker?.Start("Git:commit"))
                    return ExecuteUnless(startInfo, "nothing to commit");
            }
        }

        public void Tag(string name, string taggerName, string taggerEmail, string comment, DateTime localTime)
        {
            TempFile commentFile;

            var args = "tag";
            AddComment(comment, ref args, out commentFile);

            // tag names are not quoted because they cannot contain whitespace or quotes
            args += " -- " + name;

            using (commentFile)
            {
                var startInfo = GetStartInfo(args);
                startInfo.EnvironmentVariables["GIT_COMMITTER_NAME"] = taggerName;
                startInfo.EnvironmentVariables["GIT_COMMITTER_EMAIL"] = taggerEmail;
                startInfo.EnvironmentVariables["GIT_COMMITTER_DATE"] = GetUtcTimeString(localTime);

                using (perfTracker?.Start("Git:tag"))
                    ExecuteUnless(startInfo, null);
            }
        }

        private static string GetUtcTimeString(DateTime localTime)
        {
            // convert local time to UTC based on whether DST was in effect at the time
            var utcTime = TimeZoneInfo.ConvertTimeToUtc(localTime);

            // format time according to ISO 8601 (avoiding locale-dependent month/day names)
            return utcTime.ToString("yyyy'-'MM'-'dd HH':'mm':'ss +0000");
        }

        private void GitExec(string args)
        {
            var startInfo = GetStartInfo(args);
            ExecuteUnless(startInfo, null);
        }

        private ProcessStartInfo GetStartInfo(string args)
        {
            if (!string.IsNullOrEmpty(gitInitialArguments))
            {
                args = gitInitialArguments + " " + args;
            }

            var startInfo = new ProcessStartInfo(gitExecutable, args);
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardInput = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.WorkingDirectory = repoPath;
            startInfo.CreateNoWindow = true;
            return startInfo;
        }

        private bool ExecuteUnless(ProcessStartInfo startInfo, string unless)
        {
            string stdout, stderr;
            int exitCode = Execute(startInfo, out stdout, out stderr);
            if (exitCode != 0)
            {
                if (string.IsNullOrEmpty(unless) ||
                    ((string.IsNullOrEmpty(stdout) || !stdout.Contains(unless)) &&
                     (string.IsNullOrEmpty(stderr) || !stderr.Contains(unless))))
                {
                    FailExitCode(startInfo.FileName, startInfo.Arguments, stdout, stderr, exitCode);
                }
            }
            return exitCode == 0;
        }

        private static void FailExitCode(string exec, string args, string stdout, string stderr, int exitCode)
        {
            throw new ProcessExitException(
                string.Format("git returned exit code {0}", exitCode),
                exec, args, stdout, stderr);
        }

        private int Execute(ProcessStartInfo startInfo, out string stdout, out string stderr)
        {
            logger.WriteLine("Executing: {0} {1}", startInfo.FileName, startInfo.Arguments);
            stopwatch.Start();
            try
            {
                using (var process = Process.Start(startInfo))
                {
                    process.StandardInput.Close();
                    var stdoutReader = new AsyncLineReader(process.StandardOutput.BaseStream);
                    var stderrReader = new AsyncLineReader(process.StandardError.BaseStream);

                    var activityEvent = new ManualResetEvent(false);
                    EventHandler activityHandler = delegate { activityEvent.Set(); };
                    process.Exited += activityHandler;
                    stdoutReader.DataReceived += activityHandler;
                    stderrReader.DataReceived += activityHandler;

                    var stdoutBuffer = new StringBuilder();
                    var stderrBuffer = new StringBuilder();
                    while (true)
                    {
                        activityEvent.Reset();

                        while (true)
                        {
                            string line = stdoutReader.ReadLine();
                            if (line != null)
                            {
                                line = line.TrimEnd();
                                if (stdoutBuffer.Length > 0)
                                {
                                    stdoutBuffer.AppendLine();
                                }
                                stdoutBuffer.Append(line);
                                logger.Write('>');
                            }
                            else
                            {
                                line = stderrReader.ReadLine();
                                if (line != null)
                                {
                                    line = line.TrimEnd();
                                    if (stderrBuffer.Length > 0)
                                    {
                                        stderrBuffer.AppendLine();
                                    }
                                    stderrBuffer.Append(line);
                                    logger.Write('!');
                                }
                                else
                                {
                                    break;
                                }
                            }
                            logger.WriteLine(line);
                        }

                        if (process.HasExited)
                        {
                            break;
                        }

                        activityEvent.WaitOne(1000);
                    }

                    stdout = stdoutBuffer.ToString();
                    stderr = stderrBuffer.ToString();
                    return process.ExitCode;
                }
            }
            catch (FileNotFoundException e)
            {
                throw new ProcessException("Executable not found.",
                    e, startInfo.FileName, startInfo.Arguments);
            }
            catch (Win32Exception e)
            {
                throw new ProcessException("Error executing external process.",
                    e, startInfo.FileName, startInfo.Arguments);
            }
            finally
            {
                stopwatch.Stop();
            }
        }

        private bool FindInPathVar(string filename, out string foundPath)
        {
            string path = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(path))
            {
                return FindInPaths(filename, path.Split(Path.PathSeparator), out foundPath);
            }
            foundPath = null;
            return false;
        }

        private bool FindInPaths(string filename, IEnumerable<string> searchPaths, out string foundPath)
        {
            foreach (string searchPath in searchPaths)
            {
                string path = Path.Combine(searchPath, filename);
                if (File.Exists(path))
                {
                    foundPath = path;
                    return true;
                }
            }
            foundPath = null;
            return false;
        }

        private const char QuoteChar = '"';
        private const char EscapeChar = '\\';

        /// <summary>
        /// Puts quotes around a command-line argument if it includes whitespace
        /// or quotes.
        /// </summary>
        /// <remarks>
        /// There are two things going on in this method: quoting and escaping.
        /// Quoting puts the entire string in quotes, whereas escaping is per-
        /// character. Quoting happens only if necessary, when whitespace or a
        /// quote is encountered somewhere in the string, and escaping happens
        /// only within quoting. Spaces don't need escaping, since that's what
        /// the quotes are for. Slashes don't need escaping because apparently a
        /// backslash is only interpreted as an escape when it precedes a quote.
        /// Otherwise both slash and backslash are just interpreted as directory
        /// separators.
        /// </remarks>
        /// <param name="arg">A command-line argument to quote.</param>
        /// <returns>The given argument, possibly in quotes, with internal
        /// quotes escaped with backslashes.</returns>
        private string Quote(string arg)
        {
            if (string.IsNullOrEmpty(arg))
            {
                return "\"\"";
            }

            StringBuilder buf = null;
            for (int i = 0; i < arg.Length; ++i)
            {
                char c = arg[i];
                if (buf == null && NeedsQuoting(c))
                {
                    buf = new StringBuilder(arg.Length + 2);
                    buf.Append(QuoteChar);
                    buf.Append(arg, 0, i);
                }
                if (buf != null)
                {
                    if (c == QuoteChar)
                    {
                        buf.Append(EscapeChar);
                    }
                    buf.Append(c);
                }
            }
            if (buf != null)
            {
                buf.Append(QuoteChar);
                return buf.ToString();
            }
            return arg;
        }

        private bool NeedsQuoting(char c)
        {
            return char.IsWhiteSpace(c) || c == QuoteChar ||
                (shellQuoting && (c == '&' || c == '|' || c == '<' || c == '>' || c == '^' || c == '%'));
        }

        public void Dispose()
        {
            // GitWrapper doesn't hold any resources that need disposal
            // This method is implemented to satisfy the IGitRepository interface
        }
    }
}
