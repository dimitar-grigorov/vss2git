using CommandLine;

namespace Hpdi.Vss2Git.Cli
{
    /// <summary>
    /// Command-line options for the list command.
    /// </summary>
    [Verb("list", HelpText = "List items in a VSS database (projects, files, shared files, checkouts, stats)")]
    public class ListOptions
    {
        [Option('v', "vss-dir", Required = true, HelpText = "Path to VSS database directory (contains srcsafe.ini)")]
        public string VssDirectory { get; set; }

        [Option('p', "vss-project", Default = "$", HelpText = "Root project path to scan (default: $)")]
        public string VssProject { get; set; }

        [Option('c', "encoding", HelpText = "VSS encoding code page (e.g., 1252 for Windows-1252). Default: system default")]
        public int? EncodingCodePage { get; set; }

        [Option('t', "type", Default = "all",
            HelpText = "Item types to list: projects, files, all")]
        public string Type { get; set; }

        [Option('s', "shared", Default = false,
            HelpText = "List only shared files (referenced from multiple projects). Output is grouped by physical file.")]
        public bool SharedOnly { get; set; }

        [Option("stats", Default = false,
            HelpText = "Print database statistics: counts, date range, top contributors, top-revised files, label count. Walks all revisions.")]
        public bool Stats { get; set; }

        [Option("checkouts", Default = false,
            HelpText = "List currently checked-out files across the entire database (like 'ss Status' but global).")]
        public bool Checkouts { get; set; }

        [Option("include-deleted", Default = false,
            HelpText = "Include soft-deleted entries (default: hidden, matching VSS GUI). In --shared output, also reveals files flagged Shared but with only one live reference. Destroyed items are gone regardless.")]
        public bool IncludeDeleted { get; set; }

        [Option('f', "format", Default = "tree",
            HelpText = "Output format: tree, flat. Ignored when --shared, --stats, or --checkouts is set.")]
        public string Format { get; set; }
    }
}
