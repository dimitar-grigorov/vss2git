using CommandLine;

namespace Hpdi.Vss2Git.Cli
{
    /// <summary>
    /// Command-line options for tree command
    /// </summary>
    [Verb("tree", HelpText = "Display VSS project hierarchy as a tree")]
    public class TreeOptions
    {
        [Option('v', "vss-dir", Required = true, HelpText = "Path to VSS database directory (contains srcsafe.ini)")]
        public string VssDirectory { get; set; }

        [Option('p', "vss-project", Default = "$", HelpText = "VSS project path to display (default: $)")]
        public string VssProject { get; set; }

        [Option('c', "encoding", HelpText = "VSS encoding code page (e.g., 1252 for Windows-1252). Default: system default")]
        public int? EncodingCodePage { get; set; }

        [Option('f', "files", Default = false, HelpText = "Include files (default: projects only)")]
        public bool IncludeFiles { get; set; }
    }
}
