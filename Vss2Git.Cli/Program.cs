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

namespace Hpdi.Vss2Git.Cli
{
    class Program
    {
        static int Main(string[] args)
        {
            // Register code page encodings (required for VSS encoding support)
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            // Parse command-line arguments
            return Parser.Default.ParseArguments<CliOptions>(args)
                .MapResult(
                    options => RunMigration(options),
                    errors => 1
                );
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
            var config = CliOptionsMapper.FromOptions(options, encoding);

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

            // Check output directory
            if (!options.Force && Directory.Exists(options.GitDirectory) &&
                Directory.EnumerateFileSystemEntries(options.GitDirectory).Any())
            {
                Console.Error.WriteLine($"ERROR: Output directory is not empty: {options.GitDirectory}");
                Console.Error.WriteLine("Use --force to continue anyway.");
                return 1;
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
            Console.WriteLine();

            // Create work queue
            var workQueue = new WorkQueue(1);

            // Create UI abstractions
            var userInteraction = new ConsoleUserInteraction(options.IgnoreErrors, options.Interactive);

            // Create orchestrator (temporary status reporter)
            var orchestrator = new MigrationOrchestrator(config, workQueue, userInteraction,
                new NullStatusReporter());

            // Create status reporter with orchestrator
            var statusReporter = new ConsoleStatusReporter(workQueue, orchestrator);

            // Recreate orchestrator with real status reporter
            orchestrator = new MigrationOrchestrator(config, workQueue, userInteraction, statusReporter);

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
                return 1;
            }

            // Wait for completion
            workQueue.WaitIdle();

            Console.WriteLine();
            Console.WriteLine("========================================");
            Console.WriteLine("Migration completed successfully!");
            Console.WriteLine("========================================");

            return 0;
        }

        /// <summary>
        /// Null status reporter for initial orchestrator creation
        /// </summary>
        private class NullStatusReporter : IStatusReporter
        {
            public void Start() { }
            public void Stop() { }
        }
    }
}
