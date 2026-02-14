using CommandLine;

namespace Hpdi.Vss2Git.Cli
{
    /// <summary>
    /// Command-line options for Vss2Git migration
    /// </summary>
    [Verb("migrate", isDefault: true, HelpText = "Migrate VSS repository to Git")]
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

        [Option("any-comment-threshold", Default = 0, HelpText = "Seconds threshold for grouping revisions regardless of comment (0 = same-second only)")]
        public int AnyCommentSeconds { get; set; }

        [Option("same-comment-threshold", Default = 60, HelpText = "Seconds threshold for grouping revisions with identical comment")]
        public int SameCommentSeconds { get; set; }

        [Option('t', "transcode", Default = true, HelpText = "Transcode comments to UTF-8 (use --transcode false to disable)")]
        public bool TranscodeComments { get; set; }

        [Option("force-annotated-tags", Default = true, HelpText = "Force annotated tags (use --force-annotated-tags false to disable)")]
        public bool ForceAnnotatedTags { get; set; }

        [Option("export-to-root", Default = false, HelpText = "Export project directly to Git root")]
        public bool ExportProjectToGitRoot { get; set; }

        [Option("from-date", HelpText = "Export changesets from this date (yyyy-MM-dd or yyyy-MM-ddTHH:mm:ss). Earlier changesets build mapper state only.")]
        public string FromDate { get; set; }

        [Option("to-date", HelpText = "Export changesets up to this date (yyyy-MM-dd or yyyy-MM-ddTHH:mm:ss). Stops after last matching changeset.")]
        public string ToDate { get; set; }

        [Option("perf", Default = false, HelpText = "Enable performance tracking and print summary at end")]
        public bool EnablePerformanceTracking { get; set; }

        [Option("git-backend", Default = GitBackend.Process, HelpText = "Git backend: Process (git.exe, default) or LibGit2Sharp (managed, faster)")]
        public GitBackend GitBackend { get; set; }
    }
}
