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
        private readonly Action pauseStatus;
        private readonly Action resumeStatus;

        public ConsoleUserInteraction(bool ignoreErrors, bool interactive,
            Action pauseStatus = null, Action resumeStatus = null)
        {
            this.ignoreErrors = ignoreErrors;
            this.interactive = interactive;
            this.pauseStatus = pauseStatus;
            this.resumeStatus = resumeStatus;
        }

        public ErrorAction ReportError(string message, ErrorActionOptions options)
        {
            pauseStatus?.Invoke();
            Console.Error.WriteLine($"ERROR: {message}");

            if (ignoreErrors)
            {
                Console.Error.WriteLine("Ignoring error (--ignore-errors mode)");
                resumeStatus?.Invoke();
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

            var result = choice switch
            {
                "R" or "RETRY" => ErrorAction.Retry,
                "I" or "IGNORE" => ErrorAction.Ignore,
                _ => ErrorAction.Abort
            };

            if (result != ErrorAction.Abort)
                resumeStatus?.Invoke();

            return result;
        }

        public bool Confirm(string message, string title)
        {
            pauseStatus?.Invoke();

            Console.WriteLine($"{title}:");
            Console.WriteLine(message);
            Console.Write("Continue? [Y/N]: ");

            var input = Console.ReadLine();
            var choice = input?.Trim().ToUpperInvariant();
            var confirmed = choice == "Y" || choice == "YES";

            resumeStatus?.Invoke();
            return confirmed;
        }

        public void ShowFatalError(string message, Exception exception)
        {
            pauseStatus?.Invoke();

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
