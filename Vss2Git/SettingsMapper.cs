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
using System.Text;
using Mapster;

namespace Hpdi.Vss2Git
{
    /// <summary>
    /// Maps between Properties.Settings and MigrationConfiguration using Mapster
    /// </summary>
    public static class SettingsMapper
    {
        static SettingsMapper()
        {
            // From Settings to MigrationConfiguration
            TypeAdapterConfig<Properties.Settings, MigrationConfiguration>.NewConfig()
                .Ignore(dest => dest.VssEncoding)
                .Ignore(dest => dest.Force)
                .Ignore(dest => dest.IgnoreErrors)
                .Ignore(dest => dest.FromDate)
                .Ignore(dest => dest.ToDate)
                .Ignore(dest => dest.GitBackend);
        }

        /// <summary>
        /// Create MigrationConfiguration from persisted settings and current encoding
        /// </summary>
        public static MigrationConfiguration FromSettings(Encoding encoding)
        {
            var settings = Properties.Settings.Default;

            // Use Mapster for basic mapping
            var config = settings.Adapt<MigrationConfiguration>();

            // Set properties that aren't persisted or need special mapping
            config.VssEncoding = encoding;
            config.IgnoreErrors = false; // Not persisted, always starts as false
            config.GitBackend = Enum.TryParse<GitBackend>(settings.GitBackend, out var backend)
                ? backend : GitBackend.Process;

            return config;
        }

        /// <summary>
        /// Save MigrationConfiguration back to settings (persistence)
        /// </summary>
        public static void ToSettings(MigrationConfiguration config)
        {
            var settings = Properties.Settings.Default;

            config.Adapt(settings);

            settings.Save();
        }
    }
}
