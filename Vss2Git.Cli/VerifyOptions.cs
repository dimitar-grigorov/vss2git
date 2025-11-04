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

using CommandLine;

namespace Hpdi.Vss2Git.Cli
{
    /// <summary>
    /// Command-line options for verify command
    /// </summary>
    [Verb("verify", HelpText = "Compare source and target directories")]
    public class VerifyOptions
    {
        [Option('s', "source", Required = true, HelpText = "Source directory (VSS or expected)")]
        public string SourceDirectory { get; set; }

        [Option('t', "target", Required = true, HelpText = "Target directory (Git)")]
        public string TargetDirectory { get; set; }

        [Option('x', "exclude", HelpText = "Exclude patterns (semicolon-separated)")]
        public string ExcludePatterns { get; set; }
    }
}
