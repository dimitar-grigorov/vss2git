using System;
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
        public GitBackend GitBackend { get; set; } = GitBackend.Process;
        public string DefaultEmailDomain { get; set; } = "localhost";
        public string DefaultComment { get; set; } = "";
        public bool ExportProjectToGitRoot { get; set; } = false;

        // Changeset settings
        public int AnyCommentSeconds { get; set; } = 0;
        public int SameCommentSeconds { get; set; } = 60;

        // Transcoding settings
        public bool TranscodeComments { get; set; } = true;

        // Operational settings
        public bool Force { get; set; } = false;
        public bool ForceAnnotatedTags { get; set; } = true;
        public bool IgnoreErrors { get; set; } = false;
        public string LogFile { get; set; }

        // Date filtering
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }

        // Path mapping (selective export with renaming)
        public Dictionary<string, string> PathMappings { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Diagnostics
        public bool EnablePerformanceTracking { get; set; } = false;

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

            if (FromDate.HasValue && ToDate.HasValue && FromDate.Value > ToDate.Value)
                errors.Add("From date must be earlier than or equal to to date");

            if (PathMappings != null && PathMappings.Count > 0)
            {
                if (ExportProjectToGitRoot)
                    errors.Add("--path-map cannot be combined with --export-to-root");

                var vssPrefix = (VssProject ?? "$").TrimEnd('/');
                var gitNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in PathMappings)
                {
                    if (!kvp.Key.StartsWith(vssPrefix + "/", StringComparison.OrdinalIgnoreCase) &&
                        !kvp.Key.Equals(vssPrefix, StringComparison.OrdinalIgnoreCase))
                        errors.Add($"Path mapping key '{kvp.Key}' must be under --vss-project '{VssProject}'");

                    if (string.IsNullOrWhiteSpace(kvp.Value))
                        errors.Add($"Path mapping value for '{kvp.Key}' cannot be empty");
                    else if (kvp.Value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                        errors.Add($"Path mapping value '{kvp.Value}' contains invalid characters");

                    // Allow duplicate git names — same project may have been moved
                    // between VSS paths during history
                    gitNames.Add(kvp.Value);
                }
            }

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
