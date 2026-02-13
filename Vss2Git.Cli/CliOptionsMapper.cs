using System;
using System.Globalization;
using System.Text;
using Mapster;

namespace Hpdi.Vss2Git.Cli
{
    /// <summary>
    /// Maps between CliOptions and MigrationConfiguration using Mapster
    /// </summary>
    public static class CliOptionsMapper
    {
        static CliOptionsMapper()
        {
            // Configure mapping from CliOptions to MigrationConfiguration
            TypeAdapterConfig<CliOptions, MigrationConfiguration>.NewConfig()
                .Ignore(dest => dest.VssEncoding)    // Set separately based on EncodingCodePage
                .Ignore(dest => dest.FromDate)       // Parsed manually from string
                .Ignore(dest => dest.ToDate);        // Parsed manually from string

            // Configure mapping from MigrationConfiguration to CliOptions
            TypeAdapterConfig<MigrationConfiguration, CliOptions>.NewConfig()
                .Ignore(dest => dest.EncodingCodePage)  // Derived from VssEncoding
                .Ignore(dest => dest.Force)             // Not part of MigrationConfiguration
                .Ignore(dest => dest.Interactive)        // Not part of MigrationConfiguration
                .Ignore(dest => dest.FromDate)           // Formatted manually from DateTime
                .Ignore(dest => dest.ToDate);            // Formatted manually from DateTime
        }

        /// <summary>
        /// Create MigrationConfiguration from CLI options
        /// </summary>
        public static MigrationConfiguration FromOptions(CliOptions options, Encoding encoding)
        {
            var config = options.Adapt<MigrationConfiguration>();

            config.VssEncoding = encoding;

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
            var options = config.Adapt<CliOptions>();

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
