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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Hpdi.Vss2Git.Cli
{
    /// <summary>
    /// Simple directory comparison tool for verifying migrations
    /// </summary>
    public static class VerifyCommand
    {
        private static readonly string[] DefaultExcludes = { ".git", ".vs", "! Borland !", "! CodeGear - RAD Studio !", "ToPAZ" };

        public static int Run(string sourceDir, string targetDir, string[] excludePatterns = null)
        {
            Console.WriteLine();
            Console.WriteLine("Comparing directory structures...");
            Console.WriteLine($"Source: {sourceDir}");
            Console.WriteLine($"Target: {targetDir}");

            var excludes = excludePatterns ?? DefaultExcludes;
            Console.WriteLine($"Excluded: {string.Join(", ", excludes)}");
            Console.WriteLine();

            // Validate directories exist
            if (!Directory.Exists(sourceDir))
            {
                Console.Error.WriteLine($"ERROR: Source directory does not exist: {sourceDir}");
                return 1;
            }

            if (!Directory.Exists(targetDir))
            {
                Console.Error.WriteLine($"ERROR: Target directory does not exist: {targetDir}");
                return 1;
            }

            // Scan directories
            Console.WriteLine("Scanning source directory...");
            var sourceItems = ScanDirectory(sourceDir, excludes);

            Console.WriteLine("Scanning target directory...");
            var targetItems = ScanDirectory(targetDir, excludes);

            // Compare
            Console.WriteLine();
            Console.WriteLine("========================================");
            Console.WriteLine("COMPARISON RESULTS");
            Console.WriteLine("========================================");
            Console.WriteLine();
            Console.WriteLine($"Total items in source: {sourceItems.Count}");
            Console.WriteLine($"Total items in target: {targetItems.Count}");
            Console.WriteLine();

            var missing = sourceItems.Keys.Where(k => !targetItems.ContainsKey(k)).Select(k => sourceItems[k]).OrderBy(x => x).ToList();
            var extra = targetItems.Keys.Where(k => !sourceItems.ContainsKey(k)).Select(k => targetItems[k]).OrderBy(x => x).ToList();

            if (missing.Count == 0 && extra.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("SUCCESS: Directory structures match perfectly!");
                Console.WriteLine("All files and folders present in both directories.");
                Console.ResetColor();
                return 0;
            }

            // Report missing items
            if (missing.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Items MISSING in TARGET ({missing.Count} items):");
                Console.WriteLine("These items exist in source but are MISSING in target:");
                Console.ResetColor();
                Console.WriteLine();
                foreach (var item in missing)
                {
                    Console.WriteLine($"MISSING_IN_TARGET: {item}");
                }
                Console.WriteLine();
            }

            // Report extra items
            if (extra.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine($"Items ONLY in TARGET ({extra.Count} items):");
                Console.WriteLine("These items exist in target but are NOT in source:");
                Console.ResetColor();
                Console.WriteLine();
                foreach (var item in extra)
                {
                    Console.WriteLine($"EXTRA_IN_TARGET: {item}");
                }
                Console.WriteLine();
            }

            // Summary
            Console.WriteLine("========================================");
            Console.WriteLine("SUMMARY");
            Console.WriteLine("========================================");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Missing in target: {missing.Count} items");
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"Extra in target:   {extra.Count} items");
            Console.ResetColor();
            Console.WriteLine();

            return 1;
        }

        private static Dictionary<string, string> ScanDirectory(string baseDir, string[] excludes)
        {
            var items = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ScanDirectoryRecursive(baseDir, baseDir, items, excludes);
            return items;
        }

        private static void ScanDirectoryRecursive(string baseDir, string currentDir, Dictionary<string, string> items, string[] excludes)
        {
            var dirInfo = new DirectoryInfo(currentDir);

            // Scan files
            foreach (var file in dirInfo.GetFiles())
            {
                var relativePath = GetRelativePath(baseDir, file.FullName);
                if (!ShouldExclude(relativePath, excludes))
                {
                    items[relativePath.ToLowerInvariant()] = relativePath;
                }
            }

            // Scan subdirectories
            foreach (var dir in dirInfo.GetDirectories())
            {
                var relativePath = GetRelativePath(baseDir, dir.FullName);
                if (!ShouldExclude(relativePath, excludes))
                {
                    items[relativePath.ToLowerInvariant()] = relativePath;
                    ScanDirectoryRecursive(baseDir, dir.FullName, items, excludes);
                }
            }
        }

        private static string GetRelativePath(string baseDir, string fullPath)
        {
            var baseUri = new Uri(baseDir.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
            var fullUri = new Uri(fullPath);
            return Uri.UnescapeDataString(baseUri.MakeRelativeUri(fullUri).ToString().Replace('/', Path.DirectorySeparatorChar));
        }

        private static bool ShouldExclude(string relativePath, string[] excludes)
        {
            foreach (var exclude in excludes)
            {
                // Check if path contains the excluded pattern
                if (relativePath.IndexOf(exclude, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
