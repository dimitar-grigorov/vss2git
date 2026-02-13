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
using System.IO;
using System.Linq;
using System.Text;
using CommandLine;
using CommandLine.Text;

namespace Hpdi.Vss2Git.Cli
{
    class Program
    {
        static int Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            int width;
            try { width = Console.WindowWidth; } catch { width = 80; }

            var parser = new Parser(s => { s.HelpWriter = null; s.MaximumDisplayWidth = width; });
            var parsed = parser.ParseArguments<CliOptions, VerifyOptions>(args);

            return parsed.MapResult(
                (CliOptions o) => RunMigration(o),
                (VerifyOptions o) => RunVerify(o),
                _ => { Console.Error.WriteLine(HelpText.AutoBuild(parsed, h =>
                    { h.AdditionalNewLineAfterOption = false; return h; })); return 1; });
        }

        static int RunMigration(CliOptions options)
        {
            Console.WriteLine("VSS2Git Console - Migration Tool");
            Console.WriteLine("=================================");
            Console.WriteLine();

            // Determine encoding
            int encodingCodePage = options.EncodingCodePage ?? Encoding.Default.CodePage;
            Encoding encoding;
            try
            {
                encoding = Encoding.GetEncoding(encodingCodePage);
            }
            catch (ArgumentException)
            {
                Console.Error.WriteLine($"ERROR: Invalid encoding code page: {encodingCodePage}");
                return 1;
            }

            // Build configuration using CliOptionsMapper
            MigrationConfiguration config;
            try
            {
                config = CliOptionsMapper.FromOptions(options, encoding);
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine($"ERROR: {ex.Message}");
                return 1;
            }

            // Validate configuration
            var validation = config.Validate();
            if (!validation.IsValid)
            {
                Console.Error.WriteLine("Configuration errors:");
                foreach (var error in validation.Errors)
                {
                    Console.Error.WriteLine($"  - {error}");
                }
                return 1;
            }

            // Check output directory (skip when --from-date is set for continuation)
            if (config.FromDate == null && !options.Force && Directory.Exists(options.GitDirectory) &&
                Directory.EnumerateFileSystemEntries(options.GitDirectory).Any())
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
                var renamedDir = options.GitDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + "-" + timestamp;

                Console.Error.WriteLine($"Output directory is not empty: {options.GitDirectory}");
                Console.Write($"Rename it to \"{renamedDir}\" and continue? [y/N] ");
                var key = Console.ReadLine()?.Trim();
                if (!string.Equals(key, "y", StringComparison.OrdinalIgnoreCase))
                {
                    Console.Error.WriteLine("Aborted. Use --force or --from-date to continue.");
                    return 1;
                }

                Directory.Move(options.GitDirectory, renamedDir);
                Console.WriteLine($"Renamed to: {renamedDir}");
            }

            // Display configuration summary
            Console.WriteLine("Configuration:");
            Console.WriteLine($"  VSS Directory:     {config.VssDirectory}");
            Console.WriteLine($"  VSS Project:       {config.VssProject}");
            Console.WriteLine($"  Git Directory:     {config.GitDirectory}");
            Console.WriteLine($"  Email Domain:      {config.DefaultEmailDomain}");
            Console.WriteLine($"  Encoding:          {encoding.EncodingName} (CP: {encoding.CodePage})");
            Console.WriteLine($"  Log File:          {config.LogFile}");
            Console.WriteLine($"  Ignore Errors:     {config.IgnoreErrors}");
            Console.WriteLine($"  Interactive:       {options.Interactive}");
            if (!string.IsNullOrEmpty(config.VssExcludePaths))
            {
                Console.WriteLine($"  Exclude Patterns:  {config.VssExcludePaths}");
            }
            if (config.FromDate.HasValue)
            {
                Console.WriteLine($"  From Date:         {config.FromDate.Value:yyyy-MM-dd HH:mm:ss}");
            }
            if (config.ToDate.HasValue)
            {
                Console.WriteLine($"  To Date:           {config.ToDate.Value:yyyy-MM-dd HH:mm:ss}");
            }
            Console.WriteLine();

            // Rotate previous log file
            Logger.RotateLogFile(config.LogFile);

            // Create work queue
            var workQueue = new WorkQueue(1);

            // Create UI abstractions
            var statusReporter = new ConsoleStatusReporter(workQueue);
            var userInteraction = new ConsoleUserInteraction(
                options.IgnoreErrors, options.Interactive,
                statusReporter.Stop, statusReporter.Start);

            // Create orchestrator
            var orchestrator = new MigrationOrchestrator(config, workQueue, userInteraction, statusReporter);

            // Set orchestrator reference for detailed statistics in status reporter
            statusReporter.Orchestrator = orchestrator;

            // Handle Ctrl + C
            Console.CancelKeyPress += (sender, e) =>
            {
                Console.Error.WriteLine("\nAborting migration...");
                orchestrator.Abort();
                e.Cancel = true; // Don't terminate immediately, let WorkQueue finish
            };

            // Run migration
            Console.WriteLine("Starting migration...");
            Console.WriteLine();

            if (!orchestrator.Run())
            {
                statusReporter.Stop();
                return 1;
            }

            // Wait for completion
            workQueue.WaitIdle();
            statusReporter.Stop();

            // Display final statistics
            Console.WriteLine();
            Console.WriteLine("========================================");
            Console.WriteLine("Migration completed successfully!");
            Console.WriteLine("========================================");
            Console.WriteLine();
            Console.WriteLine("Statistics:");
            Console.WriteLine($"  Files:      {orchestrator.RevisionAnalyzer?.FileCount ?? 0}");
            Console.WriteLine($"  Revisions:  {orchestrator.RevisionAnalyzer?.RevisionCount ?? 0}");
            Console.WriteLine($"  Changesets: {orchestrator.ChangesetBuilder?.Changesets.Count ?? 0}");
            Console.WriteLine($"  Duration:   {workQueue.ActiveTime:hh\\:mm\\:ss}");

            return 0;
        }

        static int RunVerify(VerifyOptions options)
        {
            string[] excludes = null;
            if (!string.IsNullOrEmpty(options.ExcludePatterns))
            {
                excludes = options.ExcludePatterns.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            }

            return VerifyCommand.Run(options.SourceDirectory, options.TargetDirectory, excludes);
        }
    }
}
