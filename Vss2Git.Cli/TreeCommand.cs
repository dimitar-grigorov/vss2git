using System;
using System.Text;
using Hpdi.VssLogicalLib;

namespace Hpdi.Vss2Git.Cli
{
    static class TreeCommand
    {
        public static int Run(TreeOptions options)
        {
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

            var tree = VssProjectTree.Build(rootProject, options.IncludeFiles,
                warning => Console.Error.WriteLine($"  WARNING: {warning}"));

            Console.WriteLine(tree.Path + "/");
            PrintChildren(tree, "");

            var stats = VssProjectTree.Count(tree);
            Console.WriteLine();
            Console.WriteLine($"{stats.ProjectCount} projects, {stats.FileCount} files");

            return 0;
        }

        private static void PrintChildren(VssTreeNode node, string indent)
        {
            for (int i = 0; i < node.Children.Count; i++)
            {
                bool isLast = i == node.Children.Count - 1;
                var connector = isLast ? "\u2514\u2500\u2500 " : "\u251C\u2500\u2500 ";
                var childIndent = isLast ? "    " : "\u2502   ";

                var child = node.Children[i];
                var suffix = child.IsProject ? "/" : "";
                Console.WriteLine($"{indent}{connector}{child.Name}{suffix}");

                if (child.IsProject)
                {
                    PrintChildren(child, indent + childIndent);
                }
            }
        }
    }
}
