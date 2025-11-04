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

namespace Hpdi.Vss2Git.Cli
{
    /// <summary>
    /// Console-based implementation of user interaction
    /// </summary>
    public class ConsoleUserInteraction : IUserInteraction
    {
        private readonly bool ignoreErrors;
        private readonly bool interactive;

        public ConsoleUserInteraction(bool ignoreErrors, bool interactive)
        {
            this.ignoreErrors = ignoreErrors;
            this.interactive = interactive;
        }

        public ErrorAction ReportError(string message, ErrorActionOptions options)
        {
            // Move to new line if status was being displayed
            Console.WriteLine();
            Console.Error.WriteLine($"ERROR: {message}");

            if (ignoreErrors)
            {
                Console.Error.WriteLine("Ignoring error (--ignore-errors mode)");
                return ErrorAction.Ignore;
            }

            if (!interactive)
            {
                Console.Error.WriteLine("Aborting (non-interactive mode)");
                return ErrorAction.Abort;
            }

            // Prompt user for action
            Console.Error.Write("Choose action [A]bort, [R]etry, [I]gnore: ");
            var input = Console.ReadLine();

            if (string.IsNullOrEmpty(input))
                return ErrorAction.Abort;

            var choice = input.Trim().ToUpperInvariant();

            return choice switch
            {
                "R" or "RETRY" => ErrorAction.Retry,
                "I" or "IGNORE" => ErrorAction.Ignore,
                _ => ErrorAction.Abort
            };
        }

        public bool Confirm(string message, string title)
        {
            Console.WriteLine();
            Console.WriteLine($"{title}:");
            Console.WriteLine(message);
            Console.Write("Continue? [Y/N]: ");

            var input = Console.ReadLine();
            if (string.IsNullOrEmpty(input))
                return false;

            var choice = input.Trim().ToUpperInvariant();
            return choice == "Y" || choice == "YES";
        }

        public void ShowFatalError(string message, Exception exception)
        {
            Console.WriteLine();
            Console.Error.WriteLine("FATAL ERROR:");
            Console.Error.WriteLine(message);

            if (exception != null)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine("Exception details:");
                Console.Error.WriteLine(ExceptionFormatter.Format(exception));
            }
        }
    }
}
