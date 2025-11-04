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
                .Ignore(dest => dest.VssEncoding);      // Set separately based on EncodingCodePage

            // Configure mapping from MigrationConfiguration to CliOptions
            TypeAdapterConfig<MigrationConfiguration, CliOptions>.NewConfig()
                .Ignore(dest => dest.EncodingCodePage)  // Derived from VssEncoding
                .Ignore(dest => dest.Force)             // Not part of MigrationConfiguration
                .Ignore(dest => dest.Interactive);      // Not part of MigrationConfiguration
        }

        /// <summary>
        /// Create MigrationConfiguration from CLI options
        /// </summary>
        public static MigrationConfiguration FromOptions(CliOptions options, Encoding encoding)
        {
            var config = options.Adapt<MigrationConfiguration>();

            config.VssEncoding = encoding;

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

            return options;
        }
    }
}
