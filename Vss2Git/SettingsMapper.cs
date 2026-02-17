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

namespace Hpdi.Vss2Git
{
    /// <summary>
    /// Maps between Properties.Settings and MigrationConfiguration
    /// </summary>
    public static class SettingsMapper
    {
        /// <summary>
        /// Create MigrationConfiguration from persisted settings and current encoding
        /// </summary>
        public static MigrationConfiguration FromSettings(Encoding encoding)
        {
            var settings = Properties.Settings.Default;

            return new MigrationConfiguration
            {
                VssDirectory = settings.VssDirectory,
                GitDirectory = settings.GitDirectory,
                VssProject = settings.VssProject,
                VssExcludePaths = settings.VssExcludePaths,
                DefaultEmailDomain = settings.DefaultEmailDomain,
                DefaultComment = settings.DefaultComment,
                LogFile = settings.LogFile,
                TranscodeComments = settings.TranscodeComments,
                ForceAnnotatedTags = settings.ForceAnnotatedTags,
                ExportProjectToGitRoot = settings.ExportProjectToGitRoot,
                AnyCommentSeconds = settings.AnyCommentSeconds,
                SameCommentSeconds = settings.SameCommentSeconds,
                VssEncoding = encoding,
                GitBackend = Enum.TryParse<GitBackend>(settings.GitBackend, out var backend)
                    ? backend : GitBackend.Process,
            };
        }

        /// <summary>
        /// Save MigrationConfiguration back to settings (persistence)
        /// </summary>
        public static void ToSettings(MigrationConfiguration config)
        {
            var settings = Properties.Settings.Default;

            settings.VssDirectory = config.VssDirectory;
            settings.GitDirectory = config.GitDirectory;
            settings.VssProject = config.VssProject;
            settings.VssExcludePaths = config.VssExcludePaths;
            settings.DefaultEmailDomain = config.DefaultEmailDomain;
            settings.DefaultComment = config.DefaultComment;
            settings.LogFile = config.LogFile;
            settings.TranscodeComments = config.TranscodeComments;
            settings.ForceAnnotatedTags = config.ForceAnnotatedTags;
            settings.ExportProjectToGitRoot = config.ExportProjectToGitRoot;
            settings.AnyCommentSeconds = config.AnyCommentSeconds;
            settings.SameCommentSeconds = config.SameCommentSeconds;
            settings.GitBackend = config.GitBackend.ToString();
            settings.FromDate = config.FromDate?.ToString("yyyy-MM-dd") ?? "";
            settings.ToDate = config.ToDate?.ToString("yyyy-MM-dd") ?? "";

            settings.Save();
        }
    }
}
