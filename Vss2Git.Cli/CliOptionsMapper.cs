using System;
using System.Globalization;
using System.Text;

namespace Hpdi.Vss2Git.Cli
{
    /// <summary>
    /// Maps between CliOptions and MigrationConfiguration
    /// </summary>
    public static class CliOptionsMapper
    {
        /// <summary>
        /// Create MigrationConfiguration from CLI options
        /// </summary>
        public static MigrationConfiguration FromOptions(CliOptions options, Encoding encoding)
        {
            var config = new MigrationConfiguration
            {
                VssDirectory = options.VssDirectory,
                GitDirectory = options.GitDirectory,
                VssProject = options.VssProject,
                VssExcludePaths = options.VssExcludePaths,
                DefaultEmailDomain = options.DefaultEmailDomain,
                DefaultComment = options.DefaultComment,
                LogFile = options.LogFile,
                IgnoreErrors = options.IgnoreErrors,
                Force = options.Force,
                AnyCommentSeconds = options.AnyCommentSeconds,
                SameCommentSeconds = options.SameCommentSeconds,
                TranscodeComments = options.TranscodeComments,
                ForceAnnotatedTags = options.ForceAnnotatedTags,
                ExportProjectToGitRoot = options.ExportProjectToGitRoot,
                EnablePerformanceTracking = options.EnablePerformanceTracking,
                GitBackend = options.GitBackend,
                VssEncoding = encoding,
            };

            if (!string.IsNullOrEmpty(options.FromDate))
            {
                if (TryParseDate(options.FromDate, out var fromDate))
                    config.FromDate = fromDate;
                else
                    throw new ArgumentException(
                        $"Invalid --from-date format: '{options.FromDate}'. Expected yyyy-MM-dd or yyyy-MM-ddTHH:mm:ss");
            }

            if (!string.IsNullOrEmpty(options.ToDate))
            {
                if (TryParseDate(options.ToDate, out var toDate))
                    config.ToDate = toDate;
                else
                    throw new ArgumentException(
                        $"Invalid --to-date format: '{options.ToDate}'. Expected yyyy-MM-dd or yyyy-MM-ddTHH:mm:ss");
            }

            return config;
        }

        /// <summary>
        /// Create CliOptions from MigrationConfiguration (mainly for testing)
        /// </summary>
        public static CliOptions ToOptions(MigrationConfiguration config)
        {
            var options = new CliOptions
            {
                VssDirectory = config.VssDirectory,
                GitDirectory = config.GitDirectory,
                VssProject = config.VssProject,
                VssExcludePaths = config.VssExcludePaths,
                DefaultEmailDomain = config.DefaultEmailDomain,
                DefaultComment = config.DefaultComment,
                LogFile = config.LogFile,
                IgnoreErrors = config.IgnoreErrors,
                Force = config.Force,
                AnyCommentSeconds = config.AnyCommentSeconds,
                SameCommentSeconds = config.SameCommentSeconds,
                TranscodeComments = config.TranscodeComments,
                ForceAnnotatedTags = config.ForceAnnotatedTags,
                ExportProjectToGitRoot = config.ExportProjectToGitRoot,
                EnablePerformanceTracking = config.EnablePerformanceTracking,
                GitBackend = config.GitBackend,
            };

            if (config.VssEncoding != null)
            {
                options.EncodingCodePage = config.VssEncoding.CodePage;
            }

            if (config.FromDate.HasValue)
                options.FromDate = config.FromDate.Value.ToString("yyyy-MM-ddTHH:mm:ss");
            if (config.ToDate.HasValue)
                options.ToDate = config.ToDate.Value.ToString("yyyy-MM-ddTHH:mm:ss");

            return options;
        }

        private static bool TryParseDate(string value, out DateTime result)
        {
            var formats = new[] { "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd" };
            return DateTime.TryParseExact(value, formats,
                CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
        }
    }
}
