using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Hpdi.VssLogicalLib;

namespace Hpdi.Vss2Git.Cli
{
    static class ListCommand
    {
        private enum ItemType { Projects, Files, All }
        private enum OutputFormat { Tree, Flat }

        // ANSI bold: skipped when stdout is redirected (so `> file.txt` is clean)
        // or NO_COLOR is set (https://no-color.org).
        private static readonly bool ansiEnabled =
            !Console.IsOutputRedirected
            && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));

        private static string Bold(string s) =>
            ansiEnabled ? $"\x1b[1m{s}\x1b[22m" : s;

        public static int Run(ListOptions options)
        {
            if (!TryParseType(options.Type, out var type))
            {
                Console.Error.WriteLine($"ERROR: Invalid --type '{options.Type}'. Expected: projects, files, all.");
                return 1;
            }

            if (!TryParseFormat(options.Format, out var format))
            {
                Console.Error.WriteLine($"ERROR: Invalid --format '{options.Format}'. Expected: tree, flat.");
                return 1;
            }

            // --shared implies files-only
            bool sharedMode = options.SharedOnly;
            bool includeFiles = sharedMode || type != ItemType.Projects;
            bool includeProjects = !sharedMode && type != ItemType.Files;

            Encoding encoding;
            try
            {
                int codePage = options.EncodingCodePage ?? Encoding.Default.CodePage;
                encoding = Encoding.GetEncoding(codePage);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
            {
                Console.Error.WriteLine($"ERROR: Invalid encoding code page: {options.EncodingCodePage}");
                return 1;
            }

            VssDatabase db;
            try
            {
                var df = new VssDatabaseFactory(options.VssDirectory);
                df.Encoding = encoding;
                db = df.Open();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ERROR: Cannot open VSS database: {ex.Message}");
                return 1;
            }

            VssProject rootProject;
            try
            {
                var item = db.GetItem(options.VssProject);
                rootProject = item as VssProject;
                if (rootProject == null)
                {
                    Console.Error.WriteLine($"ERROR: '{options.VssProject}' is not a project");
                    return 1;
                }
            }
            catch (VssPathException ex)
            {
                Console.Error.WriteLine($"ERROR: {ex.Message}");
                return 1;
            }

            try { Console.OutputEncoding = Encoding.UTF8; } catch { }

            var tree = VssProjectTree.Build(rootProject, includeFiles, options.IncludeDeleted,
                warning => Console.Error.WriteLine($"  WARNING: {warning}"));

            if (sharedMode)
            {
                return PrintShared(tree, includeSingleRef: options.IncludeDeleted);
            }

            switch (format)
            {
                case OutputFormat.Flat:
                    PrintFlat(tree, includeProjects, includeFiles);
                    break;

                case OutputFormat.Tree:
                default:
                    PrintTree(tree, includeProjects);
                    break;
            }

            return 0;
        }

        private static void PrintTree(VssTreeNode root, bool includeProjects)
        {
            Console.WriteLine(root.Path + "/");
            PrintTreeChildren(root, "", includeProjects);

            var stats = VssProjectTree.Count(root);
            Console.WriteLine();
            Console.WriteLine($"{stats.ProjectCount} projects, {stats.FileCount} files");
        }

        private static void PrintTreeChildren(VssTreeNode node, string indent, bool includeProjects)
        {
            // Hide empty projects when listing files only, otherwise the tree
            // is dominated by empty branches.
            var visible = node.Children
                .Where(c => c.IsProject ? (includeProjects || HasFileDescendant(c)) : true)
                .ToList();

            for (int i = 0; i < visible.Count; i++)
            {
                bool isLast = i == visible.Count - 1;
                var connector = isLast ? "└── " : "├── ";
                var childIndent = isLast ? "    " : "│   ";

                var child = visible[i];
                var suffix = child.IsProject ? "/" : "";
                var marker = BuildMarker(child);
                Console.WriteLine($"{indent}{connector}{child.Name}{suffix}{marker}");

                if (child.IsProject)
                {
                    PrintTreeChildren(child, indent + childIndent, includeProjects);
                }
            }
        }

        private static bool HasFileDescendant(VssTreeNode node)
        {
            foreach (var child in node.Children)
            {
                if (!child.IsProject) return true;
                if (HasFileDescendant(child)) return true;
            }
            return false;
        }

        private static void PrintFlat(VssTreeNode root, bool includeProjects, bool includeFiles)
        {
            int projects = 0, files = 0;
            var lines = new List<string>();
            CollectFlat(root, includeProjects, includeFiles, lines, ref projects, ref files);
            lines.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (var line in lines) Console.WriteLine(line);

            Console.WriteLine();
            Console.WriteLine($"{projects} projects, {files} files");
        }

        private static void CollectFlat(VssTreeNode node, bool includeProjects, bool includeFiles,
            List<string> lines, ref int projects, ref int files)
        {
            foreach (var child in node.Children)
            {
                if (child.IsProject)
                {
                    projects++;
                    if (includeProjects) lines.Add(child.Path + "/");
                    CollectFlat(child, includeProjects, includeFiles, lines, ref projects, ref files);
                }
                else
                {
                    files++;
                    if (includeFiles) lines.Add(child.Path + BuildMarker(child));
                }
            }
        }

        private static int PrintShared(VssTreeNode root, bool includeSingleRef)
        {
            var byPhysical = new Dictionary<string, SharedEntry>(StringComparer.OrdinalIgnoreCase);
            CollectFiles(root, byPhysical);

            var multiPath = new List<SharedEntry>();
            var flaggedSinglePath = new List<SharedEntry>();
            foreach (var entry in byPhysical.Values)
            {
                if (entry.Paths.Count > 1) multiPath.Add(entry);
                else if (includeSingleRef && entry.IsShared) flaggedSinglePath.Add(entry);
            }

            // Highest fanout first; the long tail of 2-way pairs is rarely interesting.
            multiPath.Sort((a, b) =>
            {
                int byCount = b.Paths.Count.CompareTo(a.Paths.Count);
                return byCount != 0 ? byCount
                    : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            flaggedSinglePath.Sort((a, b) =>
                string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

            int totalRefs = multiPath.Sum(e => e.Paths.Count) + flaggedSinglePath.Count;
            int shared = multiPath.Count + flaggedSinglePath.Count;

            string scope = root.Path;
            string title = shared == 0
                ? $"No shared files found in {scope}"
                : $"Shared files in {scope} — {shared} file{(shared == 1 ? "" : "s")}, {totalRefs} reference{(totalRefs == 1 ? "" : "s")}";
            Console.WriteLine(title);
            Console.WriteLine(new string('═', Math.Min(title.Length, 78)));
            Console.WriteLine();

            if (shared == 0) return 0;

            foreach (var entry in multiPath)
            {
                Console.WriteLine($"{Bold(entry.Name)}  [{entry.PhysicalName}]");
                entry.Paths.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Path, b.Path));
                foreach (var p in entry.Paths)
                {
                    var del = p.IsDeleted ? "  [deleted]" : "";
                    Console.WriteLine($"  {GetParentPath(p.Path)}{del}");
                }
                Console.WriteLine();
            }

            if (flaggedSinglePath.Count > 0)
            {
                Console.WriteLine($"Files flagged Shared in VSS but with only one path inside {scope}");
                Console.WriteLine("(other references exist outside the scan root, or are soft-deleted — try --include-deleted):");
                Console.WriteLine();
                foreach (var entry in flaggedSinglePath)
                {
                    var p = entry.Paths[0];
                    var del = p.IsDeleted ? "  [deleted]" : "";
                    Console.WriteLine($"{Bold(entry.Name)}  [{entry.PhysicalName}]");
                    Console.WriteLine($"  {GetParentPath(p.Path)}{del}");
                    Console.WriteLine();
                }
            }

            Console.WriteLine($"{shared} shared file{(shared == 1 ? "" : "s")}, {totalRefs} reference{(totalRefs == 1 ? "" : "s")}");
            return 0;
        }

        private static string GetParentPath(string path)
        {
            int slash = path.LastIndexOf('/');
            if (slash <= 0) return path;
            return path.Substring(0, slash + 1);
        }

        private static void CollectFiles(VssTreeNode node, Dictionary<string, SharedEntry> map)
        {
            foreach (var child in node.Children)
            {
                if (child.IsProject)
                {
                    CollectFiles(child, map);
                }
                else
                {
                    var key = child.PhysicalName ?? child.Path;
                    if (!map.TryGetValue(key, out var entry))
                    {
                        entry = new SharedEntry
                        {
                            Name = child.Name,
                            PhysicalName = child.PhysicalName ?? "",
                            IsShared = child.IsShared,
                        };
                        map[key] = entry;
                    }
                    else
                    {
                        entry.IsShared = entry.IsShared || child.IsShared;
                    }
                    entry.Paths.Add((child.Path, child.IsDeleted));
                }
            }
        }

        private class SharedEntry
        {
            public string Name;
            public string PhysicalName;
            public bool IsShared;
            public List<(string Path, bool IsDeleted)> Paths = new List<(string, bool)>();
        }

        private static string BuildMarker(VssTreeNode node)
        {
            var parts = new List<string>(2);
            if (!node.IsProject && node.IsShared) parts.Add("shared");
            if (node.IsDeleted) parts.Add("deleted");
            return parts.Count == 0 ? "" : "  [" + string.Join(", ", parts) + "]";
        }

        private static bool TryParseType(string raw, out ItemType type)
        {
            switch ((raw ?? "").Trim().ToLowerInvariant())
            {
                case "":
                case "projects":
                    type = ItemType.Projects; return true;
                case "files":
                    type = ItemType.Files; return true;
                case "all":
                    type = ItemType.All; return true;
                default:
                    type = ItemType.Projects; return false;
            }
        }

        private static bool TryParseFormat(string raw, out OutputFormat format)
        {
            switch ((raw ?? "").Trim().ToLowerInvariant())
            {
                case "":
                case "tree":
                    format = OutputFormat.Tree; return true;
                case "flat":
                    format = OutputFormat.Flat; return true;
                default:
                    format = OutputFormat.Tree; return false;
            }
        }
    }
}
