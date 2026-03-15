using System;
using System.Collections.Generic;
using Hpdi.VssLogicalLib;

namespace Hpdi.Vss2Git
{
    /// <summary>
    /// A node in the VSS project/file tree. Usable for CLI display, GUI TreeView binding, etc.
    /// </summary>
    public class VssTreeNode
    {
        public string Name { get; }
        public string Path { get; }
        public bool IsProject { get; }
        public List<VssTreeNode> Children { get; } = new List<VssTreeNode>();

        public VssTreeNode(string name, string path, bool isProject)
        {
            Name = name;
            Path = path;
            IsProject = isProject;
        }
    }

    /// <summary>
    /// Builds an in-memory tree from a VSS project hierarchy.
    /// </summary>
    public static class VssProjectTree
    {
        public static VssTreeNode Build(VssProject root, bool includeFiles = false, Action<string> onWarning = null)
        {
            var node = new VssTreeNode(root.Name, root.Path, isProject: true);
            AddChildren(node, root, includeFiles, onWarning);
            return node;
        }

        /// <summary>
        /// Counts projects and files in a built tree (excludes the root node itself).
        /// </summary>
        public static (int ProjectCount, int FileCount) Count(VssTreeNode root)
        {
            int projects = 0, files = 0;
            CountRecursive(root, ref projects, ref files);
            return (projects, files);
        }

        private static void CountRecursive(VssTreeNode node, ref int projects, ref int files)
        {
            foreach (var child in node.Children)
            {
                if (child.IsProject)
                    projects++;
                else
                    files++;
                CountRecursive(child, ref projects, ref files);
            }
        }

        private static void AddChildren(VssTreeNode node, VssProject project, bool includeFiles, Action<string> onWarning)
        {
            foreach (var sub in SafeEnumerate(project.Projects, onWarning))
            {
                var child = new VssTreeNode(sub.Name, sub.Path, isProject: true);
                node.Children.Add(child);
                AddChildren(child, sub, includeFiles, onWarning);
            }

            if (includeFiles)
            {
                foreach (var file in SafeEnumerate(project.Files, onWarning))
                {
                    node.Children.Add(new VssTreeNode(file.Name, file.GetPath(project), isProject: false));
                }
            }
        }

        private static List<T> SafeEnumerate<T>(IEnumerable<T> source, Action<string> onWarning)
        {
            var result = new List<T>();
            try
            {
                foreach (var item in source)
                {
                    try
                    {
                        result.Add(item);
                    }
                    catch (Exception ex)
                    {
                        onWarning?.Invoke($"Error reading item: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                onWarning?.Invoke($"Error enumerating: {ex.Message}");
            }
            return result;
        }
    }
}
