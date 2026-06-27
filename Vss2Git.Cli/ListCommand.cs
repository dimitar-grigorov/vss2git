using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Hpdi.VssLogicalLib;
using Hpdi.VssPhysicalLib;

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
            int modeCount = (options.SharedOnly ? 1 : 0) + (options.Stats ? 1 : 0) + (options.Checkouts ? 1 : 0);
            if (modeCount > 1)
            {
                Console.Error.WriteLine("ERROR: --shared, --stats, and --checkouts are mutually exclusive.");
                return 1;
            }

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

            // --shared and --checkouts imply files-only; --stats walks both regardless of --type.
            bool sharedMode = options.SharedOnly;
            bool includeFiles = sharedMode || options.Checkouts || options.Stats || type != ItemType.Projects;
            bool includeProjects = !sharedMode && !options.Checkouts && type != ItemType.Files;

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

            if (options.Stats)
            {
                return PrintStats(rootProject, options.IncludeDeleted);
            }

            if (options.Checkouts)
            {
                return PrintCheckouts(rootProject, options.IncludeDeleted);
            }

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

        private static int PrintCheckouts(VssProject root, bool includeDeleted)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rows = new List<CheckoutRow>();
            WalkProjects(root, includeDeleted, project =>
            {
                foreach (var entry in SafeFileEntries(project))
                {
                    if (entry.IsDeleted && !includeDeleted) continue;
                    var file = entry.File;
                    if (!seen.Add(file.PhysicalName)) continue;
                    if (!file.IsCheckedOut) continue;

                    var checkout = TryGetCheckout(file);
                    rows.Add(new CheckoutRow
                    {
                        Path = file.GetPath(project),
                        PhysicalName = file.PhysicalName,
                        User = checkout?.User ?? "",
                        DateTime = checkout?.DateTime ?? default,
                        Machine = checkout?.Machine ?? "",
                        Exclusive = checkout?.Exclusive ?? false,
                    });
                }
            });

            rows.Sort((a, b) =>
            {
                int byUser = string.Compare(a.User, b.User, StringComparison.OrdinalIgnoreCase);
                return byUser != 0 ? byUser : string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase);
            });

            string scope = root.Path;
            string title = rows.Count == 0
                ? $"No checked-out files found in {scope}"
                : $"Checked-out files in {scope} — {rows.Count} file{(rows.Count == 1 ? "" : "s")}";
            Console.WriteLine(title);
            Console.WriteLine(new string('═', Math.Min(title.Length, 78)));
            Console.WriteLine();

            if (rows.Count == 0) return 0;

            string lastUser = null;
            foreach (var row in rows)
            {
                if (!string.Equals(row.User, lastUser, StringComparison.OrdinalIgnoreCase))
                {
                    if (lastUser != null) Console.WriteLine();
                    Console.WriteLine($"{Bold(string.IsNullOrEmpty(row.User) ? "(unknown user)" : row.User)}");
                    lastUser = row.User;
                }
                var when = row.DateTime == default ? "" : $"  {row.DateTime:yyyy-MM-dd HH:mm}";
                var machine = string.IsNullOrEmpty(row.Machine) ? "" : $"  ({row.Machine})";
                var excl = row.Exclusive ? "  [exclusive]" : "";
                Console.WriteLine($"  {row.Path}{when}{machine}{excl}");
            }

            Console.WriteLine();
            Console.WriteLine($"{rows.Count} checked-out file{(rows.Count == 1 ? "" : "s")}");
            return 0;
        }

        private static int PrintStats(VssProject root, bool includeDeleted)
        {
            int projectCount = 0;
            int fileCount = 0;
            int sharedCount = 0;
            int deletedFileEntries = 0;
            int checkedOut = 0;
            long totalRevisions = 0;
            int labelCount = 0;
            DateTime minDate = DateTime.MaxValue;
            DateTime maxDate = DateTime.MinValue;
            var authors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var authorRevisions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var topRevised = new List<(string Path, int Count)>();
            var seenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            Console.Write("Walking database");
            int dot = 0;

            WalkProjects(root, includeDeleted, project =>
            {
                projectCount++;
                foreach (var entry in SafeFileEntries(project))
                {
                    if (entry.IsDeleted) deletedFileEntries++;
                    if (entry.IsDeleted && !includeDeleted) continue;
                    var file = entry.File;
                    if (!seenFiles.Add(file.PhysicalName)) continue;

                    fileCount++;
                    if (file.IsShared) sharedCount++;
                    if (file.IsCheckedOut) checkedOut++;

                    int revs = 0;
                    try
                    {
                        foreach (var rev in file.Revisions)
                        {
                            revs++;
                            totalRevisions++;
                            if (rev.DateTime < minDate) minDate = rev.DateTime;
                            if (rev.DateTime > maxDate) maxDate = rev.DateTime;
                            if (!string.IsNullOrEmpty(rev.User))
                            {
                                authors.Add(rev.User);
                                authorRevisions.TryGetValue(rev.User, out var prior);
                                authorRevisions[rev.User] = prior + 1;
                            }
                            if (rev.Action is VssLabelAction) labelCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"  WARNING: failed reading revisions for {file.PhysicalName}: {ex.Message}");
                    }

                    topRevised.Add((file.GetPath(project), revs));

                    if (++dot % 200 == 0) Console.Write(".");
                }
            });

            Console.WriteLine();
            Console.WriteLine();

            string scope = root.Path;
            string title = $"Database statistics for {scope}";
            Console.WriteLine(title);
            Console.WriteLine(new string('═', Math.Min(title.Length, 78)));
            Console.WriteLine();

            Console.WriteLine($"  Projects:       {projectCount}");
            Console.WriteLine($"  Files:          {fileCount}  (shared: {sharedCount}, checked out: {checkedOut}, soft-deleted refs: {deletedFileEntries})");
            Console.WriteLine($"  Revisions:      {totalRevisions}");
            Console.WriteLine($"  Labels:         {labelCount}");
            Console.WriteLine($"  Authors:        {authors.Count}");
            if (minDate != DateTime.MaxValue)
            {
                Console.WriteLine($"  First activity: {minDate:yyyy-MM-dd}");
                Console.WriteLine($"  Last activity:  {maxDate:yyyy-MM-dd}");
            }
            Console.WriteLine();

            var topAuthors = authorRevisions
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .Take(10)
                .ToList();
            if (topAuthors.Count > 0)
            {
                Console.WriteLine($"Top contributors (by revision count):");
                int maxNameLen = topAuthors.Max(kv => kv.Key.Length);
                foreach (var kv in topAuthors)
                {
                    Console.WriteLine($"  {kv.Key.PadRight(maxNameLen)}  {kv.Value}");
                }
                Console.WriteLine();
            }

            var topFiles = topRevised
                .OrderByDescending(t => t.Count)
                .ThenBy(t => t.Path, StringComparer.OrdinalIgnoreCase)
                .Take(10)
                .ToList();
            if (topFiles.Count > 0)
            {
                Console.WriteLine($"Most-revised files:");
                int maxCountLen = topFiles.Max(t => t.Count.ToString().Length);
                foreach (var t in topFiles)
                {
                    Console.WriteLine($"  {t.Count.ToString().PadLeft(maxCountLen)}  {t.Path}");
                }
                Console.WriteLine();
            }

            return 0;
        }

        private static void WalkProjects(VssProject project, bool includeDeleted, Action<VssProject> visit)
        {
            visit(project);
            IEnumerable<(VssProject Project, bool IsDeleted)> children;
            try { children = project.ProjectEntries.ToList(); }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  WARNING: failed reading {project.Path}: {ex.Message}");
                return;
            }
            foreach (var child in children)
            {
                if (child.IsDeleted && !includeDeleted) continue;
                WalkProjects(child.Project, includeDeleted, visit);
            }
        }

        private static IEnumerable<(VssFile File, bool IsDeleted)> SafeFileEntries(VssProject project)
        {
            List<(VssFile, bool)> result;
            try { result = project.FileEntries.ToList(); }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  WARNING: failed reading files in {project.Path}: {ex.Message}");
                yield break;
            }
            foreach (var entry in result) yield return entry;
        }

        private static CheckoutRecord TryGetCheckout(VssFile file)
        {
            try { return file.GetCurrentCheckout(); }
            catch { return null; }
        }

        private struct CheckoutRow
        {
            public string Path;
            public string PhysicalName;
            public string User;
            public DateTime DateTime;
            public string Machine;
            public bool Exclusive;
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
