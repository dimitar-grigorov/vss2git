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

            var tree = VssProjectTree.Build(rootProject, includeFiles,
                warning => Console.Error.WriteLine($"  WARNING: {warning}"));

            if (sharedMode)
            {
                return PrintShared(tree);
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
                var marker = !child.IsProject && child.IsShared ? "  [shared]" : "";
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
                    if (includeFiles)
                    {
                        var marker = child.IsShared ? "  [shared]" : "";
                        lines.Add(child.Path + marker);
                    }
                }
            }
        }

        private static int PrintShared(VssTreeNode root)
        {
            var byPhysical = new Dictionary<string, SharedEntry>(StringComparer.OrdinalIgnoreCase);
            CollectFiles(root, byPhysical);

            var multiPath = new List<SharedEntry>();
            var flaggedSinglePath = new List<SharedEntry>();
            foreach (var entry in byPhysical.Values)
            {
                if (entry.Paths.Count > 1) multiPath.Add(entry);
                else if (entry.IsShared) flaggedSinglePath.Add(entry);
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
                Console.WriteLine($"[{entry.PhysicalName}] {entry.Name} × {entry.Paths.Count}");
                entry.Paths.Sort(StringComparer.OrdinalIgnoreCase);
                foreach (var p in entry.Paths)
                {
                    Console.WriteLine($"  {GetParentPath(p)}");
                }
                Console.WriteLine();
            }

            if (flaggedSinglePath.Count > 0)
            {
                Console.WriteLine($"Files flagged Shared in VSS but with only one path inside {scope}");
                Console.WriteLine("(other references exist outside the scan root):");
                Console.WriteLine();
                foreach (var entry in flaggedSinglePath)
                {
                    Console.WriteLine($"[{entry.PhysicalName}] {entry.Paths[0]}");
                }
                Console.WriteLine();
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
                    entry.Paths.Add(child.Path);
                }
            }
        }

        private class SharedEntry
        {
            public string Name;
            public string PhysicalName;
            public bool IsShared;
            public List<string> Paths = new List<string>();
        }

        private static bool TryParseType(string raw, out ItemType type)
        {
            switch ((raw ?? "").Trim().ToLowerInvariant())
            {
                case "":
                case "projects":
                case "project":
                case "dirs":
                case "dir":
                case "p":
                    type = ItemType.Projects; return true;
                case "files":
                case "file":
                case "f":
                    type = ItemType.Files; return true;
                case "all":
                case "both":
                case "a":
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
                case "t":
                    format = OutputFormat.Tree; return true;
                case "flat":
                case "list":
                case "l":
                    format = OutputFormat.Flat; return true;
                default:
                    format = OutputFormat.Tree; return false;
            }
        }
    }
}
