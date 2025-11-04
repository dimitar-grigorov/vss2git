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

using CommandLine;

namespace Hpdi.Vss2Git.Cli
{
    /// <summary>
    /// Command-line options for Vss2Git migration
    /// </summary>
    public class CliOptions
    {
        [Option('v', "vss-dir", Required = true, HelpText = "Path to VSS database directory (contains srcsafe.ini)")]
        public string VssDirectory { get; set; }

        [Option('g', "git-dir", Required = true, HelpText = "Output directory for Git repository")]
        public string GitDirectory { get; set; }

        [Option('p', "vss-project", Default = "$", HelpText = "VSS project path to export (default: $)")]
        public string VssProject { get; set; }

        [Option('e', "exclude", HelpText = "Exclude file patterns (semicolon-separated)")]
        public string VssExcludePaths { get; set; }

        [Option('d', "email-domain", Default = "localhost", HelpText = "Email domain for generated email addresses")]
        public string DefaultEmailDomain { get; set; }

        [Option("default-comment", Default = "", HelpText = "Default comment for changesets with no comment")]
        public string DefaultComment { get; set; }

        [Option('c', "encoding", HelpText = "VSS encoding code page (e.g., 1252 for Windows-1252). Default: system default")]
        public int? EncodingCodePage { get; set; }

        [Option('l', "log", Default = "Vss2Git.log", HelpText = "Path to log file")]
        public string LogFile { get; set; }

        [Option('i', "ignore-errors", Default = false, HelpText = "Ignore errors and continue migration")]
        public bool IgnoreErrors { get; set; }

        [Option('f', "force", Default = false, HelpText = "Continue even if output directory is not empty")]
        public bool Force { get; set; }

        [Option("interactive", Default = false, HelpText = "Prompt for user input on errors (default: abort on error)")]
        public bool Interactive { get; set; }

        [Option("any-comment-threshold", Default = 30, HelpText = "Seconds threshold for grouping revisions with any comment")]
        public int AnyCommentSeconds { get; set; }

        [Option("same-comment-threshold", Default = 600, HelpText = "Seconds threshold for grouping revisions with same comment")]
        public int SameCommentSeconds { get; set; }

        [Option('t', "transcode", Default = true, HelpText = "Transcode comments to UTF-8 (use --transcode false to disable)")]
        public bool TranscodeComments { get; set; }

        [Option("force-annotated-tags", Default = true, HelpText = "Force annotated tags (use --force-annotated-tags false to disable)")]
        public bool ForceAnnotatedTags { get; set; }

        [Option("export-to-root", Default = false, HelpText = "Export project directly to Git root")]
        public bool ExportProjectToGitRoot { get; set; }
    }
}
