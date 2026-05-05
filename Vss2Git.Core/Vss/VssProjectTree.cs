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
        public string Name { get; init; }
        public string Path { get; init; }
        public bool IsProject { get; init; }
        public string PhysicalName { get; init; }
        public bool IsShared { get; init; }
        public bool IsDeleted { get; init; }
        public List<VssTreeNode> Children { get; } = new List<VssTreeNode>();
    }

    /// <summary>
    /// Builds an in-memory tree from a VSS project hierarchy.
    /// </summary>
    public static class VssProjectTree
    {
        public static VssTreeNode Build(VssProject root, bool includeFiles = false,
            bool includeDeleted = false, Action<string> onWarning = null)
        {
            var node = new VssTreeNode { Name = root.Name, Path = root.Path, IsProject = true };
            AddChildren(node, root, includeFiles, includeDeleted, onWarning);
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

        private static void AddChildren(VssTreeNode node, VssProject project,
            bool includeFiles, bool includeDeleted, Action<string> onWarning)
        {
            foreach (var entry in SafeEnumerate(project.ProjectEntries, onWarning))
            {
                if (entry.IsDeleted && !includeDeleted) continue;
                var child = new VssTreeNode
                {
                    Name = entry.Project.Name,
                    Path = entry.Project.Path,
                    IsProject = true,
                    PhysicalName = entry.Project.PhysicalName,
                    IsDeleted = entry.IsDeleted,
                };
                node.Children.Add(child);
                AddChildren(child, entry.Project, includeFiles, includeDeleted, onWarning);
            }

            if (includeFiles)
            {
                foreach (var entry in SafeEnumerate(project.FileEntries, onWarning))
                {
                    if (entry.IsDeleted && !includeDeleted) continue;
                    node.Children.Add(new VssTreeNode
                    {
                        Name = entry.File.Name,
                        Path = entry.File.GetPath(project),
                        IsProject = false,
                        PhysicalName = entry.File.PhysicalName,
                        IsShared = entry.File.IsShared,
                        IsDeleted = entry.IsDeleted,
                    });
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
