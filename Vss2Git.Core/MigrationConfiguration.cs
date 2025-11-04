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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Hpdi.Vss2Git
{
    /// <summary>
    /// Strongly-typed configuration for VSS to Git migration
    /// </summary>
    public class MigrationConfiguration
    {
        // VSS settings
        public string VssDirectory { get; set; }
        public string VssProject { get; set; } = "$"; // Default to root
        public string VssExcludePaths { get; set; }
        public Encoding VssEncoding { get; set; } = Encoding.Default;

        // Git settings
        public string GitDirectory { get; set; }
        public string DefaultEmailDomain { get; set; } = "localhost";
        public string DefaultComment { get; set; } = "";
        public bool ExportProjectToGitRoot { get; set; } = false;

        // Changeset settings
        public int AnyCommentSeconds { get; set; } = 30;
        public int SameCommentSeconds { get; set; } = 600;

        // Transcoding settings
        public bool TranscodeComments { get; set; } = true;

        // Operational settings
        public bool ForceAnnotatedTags { get; set; } = true;
        public bool IgnoreErrors { get; set; } = false;
        public string LogFile { get; set; }

        /// <summary>
        /// Validate the configuration and return validation result
        /// </summary>
        public ValidationResult Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(VssDirectory))
                errors.Add("VSS directory is required");
            else if (!Directory.Exists(VssDirectory))
                errors.Add($"VSS directory does not exist: {VssDirectory}");

            if (string.IsNullOrWhiteSpace(GitDirectory))
                errors.Add("Git output directory is required");

            if (string.IsNullOrWhiteSpace(VssProject))
                VssProject = "$"; // Auto-fix: default to root

            if (AnyCommentSeconds < 0)
                errors.Add("Any comment threshold must be non-negative");

            if (SameCommentSeconds < 0)
                errors.Add("Same comment threshold must be non-negative");

            return new ValidationResult(errors.Count == 0, errors);
        }
    }

    /// <summary>
    /// Result of configuration validation
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; }
        public IReadOnlyList<string> Errors { get; }

        public ValidationResult(bool isValid, List<string> errors)
        {
            IsValid = isValid;
            Errors = errors?.AsReadOnly() ?? new List<string>().AsReadOnly();
        }

        public override string ToString()
        {
            if (IsValid)
                return "Configuration is valid";

            return $"Configuration has {Errors.Count} error(s):\n" +
                   string.Join("\n", Errors.Select(e => $"  - {e}"));
        }
    }
}
