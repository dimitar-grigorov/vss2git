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
using FluentAssertions;
using Hpdi.Vss2Git.Properties;
using Xunit;

namespace Hpdi.Vss2Git.Tests
{
    /// <summary>
    /// Tests for SettingsMapper class following the naming convention:
    /// MethodName_Scenario_ExpectedBehavior
    ///
    /// Note: These tests interact with the actual Settings.Default singleton.
    /// Tests save/restore original values to avoid side effects.
    /// </summary>
    public class SettingsMapperTests : IDisposable
    {
        private readonly string _originalVssDirectory;
        private readonly string _originalGitDirectory;
        private readonly string _originalVssProject;
        private readonly string _originalVssExcludePaths;
        private readonly string _originalDefaultEmailDomain;
        private readonly string _originalDefaultComment;
        private readonly string _originalLogFile;
        private readonly bool _originalTranscodeComments;
        private readonly bool _originalForceAnnotatedTags;
        private readonly bool _originalExportProjectToGitRoot;
        private readonly int _originalAnyCommentSeconds;
        private readonly int _originalSameCommentSeconds;

        public SettingsMapperTests()
        {
            // Save original settings to restore after each test
            _originalVssDirectory = Settings.Default.VssDirectory;
            _originalGitDirectory = Settings.Default.GitDirectory;
            _originalVssProject = Settings.Default.VssProject;
            _originalVssExcludePaths = Settings.Default.VssExcludePaths;
            _originalDefaultEmailDomain = Settings.Default.DefaultEmailDomain;
            _originalDefaultComment = Settings.Default.DefaultComment;
            _originalLogFile = Settings.Default.LogFile;
            _originalTranscodeComments = Settings.Default.TranscodeComments;
            _originalForceAnnotatedTags = Settings.Default.ForceAnnotatedTags;
            _originalExportProjectToGitRoot = Settings.Default.ExportProjectToGitRoot;
            _originalAnyCommentSeconds = Settings.Default.AnyCommentSeconds;
            _originalSameCommentSeconds = Settings.Default.SameCommentSeconds;
        }

        public void Dispose()
        {
            // Restore original settings after each test
            Settings.Default.VssDirectory = _originalVssDirectory;
            Settings.Default.GitDirectory = _originalGitDirectory;
            Settings.Default.VssProject = _originalVssProject;
            Settings.Default.VssExcludePaths = _originalVssExcludePaths;
            Settings.Default.DefaultEmailDomain = _originalDefaultEmailDomain;
            Settings.Default.DefaultComment = _originalDefaultComment;
            Settings.Default.LogFile = _originalLogFile;
            Settings.Default.TranscodeComments = _originalTranscodeComments;
            Settings.Default.ForceAnnotatedTags = _originalForceAnnotatedTags;
            Settings.Default.ExportProjectToGitRoot = _originalExportProjectToGitRoot;
            Settings.Default.AnyCommentSeconds = _originalAnyCommentSeconds;
            Settings.Default.SameCommentSeconds = _originalSameCommentSeconds;
        }

        #region FromSettings Tests

        [Fact]
        public void FromSettings_WithConfiguredSettings_MapsAllProperties()
        {
            // Arrange
            Settings.Default.VssDirectory = @"C:\VSS\MyRepo";
            Settings.Default.GitDirectory = @"C:\Git\MyRepo";
            Settings.Default.VssProject = "$/MyProject";
            Settings.Default.VssExcludePaths = "*.exe;*.dll";
            Settings.Default.DefaultEmailDomain = "example.com";
            Settings.Default.DefaultComment = "No comment";
            Settings.Default.LogFile = "test.log";
            Settings.Default.AnyCommentSeconds = 60;
            Settings.Default.SameCommentSeconds = 300;
            Settings.Default.TranscodeComments = false;
            Settings.Default.ForceAnnotatedTags = false;
            Settings.Default.ExportProjectToGitRoot = true;

            var encoding = Encoding.UTF8;

            // Act
            var config = SettingsMapper.FromSettings(encoding);

            // Assert
            config.VssDirectory.Should().Be(@"C:\VSS\MyRepo");
            config.GitDirectory.Should().Be(@"C:\Git\MyRepo");
            config.VssProject.Should().Be("$/MyProject");
            config.VssExcludePaths.Should().Be("*.exe;*.dll");
            config.DefaultEmailDomain.Should().Be("example.com");
            config.DefaultComment.Should().Be("No comment");
            config.LogFile.Should().Be("test.log");
            config.AnyCommentSeconds.Should().Be(60);
            config.SameCommentSeconds.Should().Be(300);
            config.TranscodeComments.Should().BeFalse();
            config.ForceAnnotatedTags.Should().BeFalse();
            config.ExportProjectToGitRoot.Should().BeTrue();
        }

        [Fact]
        public void FromSettings_WithEncoding_SetsVssEncodingCorrectly()
        {
            // Arrange
            var encoding = Encoding.GetEncoding(1252); // Windows-1252

            // Act
            var config = SettingsMapper.FromSettings(encoding);

            // Assert
            config.VssEncoding.Should().BeSameAs(encoding);
            config.VssEncoding.CodePage.Should().Be(1252);
        }

        [Fact]
        public void FromSettings_Always_SetsIgnoreErrorsToFalse()
        {
            // Arrange
            var encoding = Encoding.Default;

            // Act
            var config = SettingsMapper.FromSettings(encoding);

            // Assert
            config.IgnoreErrors.Should().BeFalse("IgnoreErrors is not persisted and should always start as false");
        }

        [Fact]
        public void FromSettings_WithDefaultSettings_UsesDefaultValues()
        {
            // Arrange - Reset to defaults
            Settings.Default.VssProject = "$";
            Settings.Default.DefaultEmailDomain = "localhost";
            Settings.Default.DefaultComment = "";
            Settings.Default.AnyCommentSeconds = 30;
            Settings.Default.SameCommentSeconds = 600;
            Settings.Default.TranscodeComments = true;
            Settings.Default.ForceAnnotatedTags = true;
            Settings.Default.ExportProjectToGitRoot = false;

            var encoding = Encoding.Default;

            // Act
            var config = SettingsMapper.FromSettings(encoding);

            // Assert
            config.VssProject.Should().Be("$");
            config.DefaultEmailDomain.Should().Be("localhost");
            config.DefaultComment.Should().Be("");
            config.AnyCommentSeconds.Should().Be(30);
            config.SameCommentSeconds.Should().Be(600);
            config.TranscodeComments.Should().BeTrue();
            config.ForceAnnotatedTags.Should().BeTrue();
            config.ExportProjectToGitRoot.Should().BeFalse();
            config.IgnoreErrors.Should().BeFalse();
        }

        [Fact]
        public void FromSettings_WithDifferentEncodings_PreservesEncoding()
        {
            // Arrange
            var testEncodings = new[]
            {
                Encoding.UTF8,
                Encoding.Unicode,
                Encoding.ASCII,
                Encoding.GetEncoding(1252),  // Windows-1252
                Encoding.GetEncoding(932),   // Japanese Shift-JIS
            };

            foreach (var encoding in testEncodings)
            {
                // Act
                var config = SettingsMapper.FromSettings(encoding);

                // Assert
                config.VssEncoding.Should().BeSameAs(encoding,
                    $"because encoding {encoding.EncodingName} should be preserved");
            }
        }

        #endregion

        #region ToSettings Tests

        [Fact]
        public void ToSettings_WithValidConfig_SavesAllProperties()
        {
            // Arrange
            var config = new MigrationConfiguration
            {
                VssDirectory = @"C:\VSS\TestRepo",
                GitDirectory = @"C:\Git\TestRepo",
                VssProject = "$/TestProject",
                VssExcludePaths = "*.tmp;*.bak",
                DefaultEmailDomain = "test.com",
                DefaultComment = "Test comment",
                LogFile = "test.log",
                AnyCommentSeconds = 45,
                SameCommentSeconds = 450,
                TranscodeComments = false,
                ForceAnnotatedTags = false,
                ExportProjectToGitRoot = true,
                VssEncoding = Encoding.UTF8,
                IgnoreErrors = true  // Should not be saved
            };

            // Act
            SettingsMapper.ToSettings(config);

            // Assert
            Settings.Default.VssDirectory.Should().Be(@"C:\VSS\TestRepo");
            Settings.Default.GitDirectory.Should().Be(@"C:\Git\TestRepo");
            Settings.Default.VssProject.Should().Be("$/TestProject");
            Settings.Default.VssExcludePaths.Should().Be("*.tmp;*.bak");
            Settings.Default.DefaultEmailDomain.Should().Be("test.com");
            Settings.Default.DefaultComment.Should().Be("Test comment");
            Settings.Default.LogFile.Should().Be("test.log");
            Settings.Default.AnyCommentSeconds.Should().Be(45);
            Settings.Default.SameCommentSeconds.Should().Be(450);
            Settings.Default.TranscodeComments.Should().BeFalse();
            Settings.Default.ForceAnnotatedTags.Should().BeFalse();
            Settings.Default.ExportProjectToGitRoot.Should().BeTrue();
        }

        [Fact]
        public void ToSettings_DoesNotThrow()
        {
            // Arrange
            var config = new MigrationConfiguration
            {
                VssDirectory = @"C:\VSS",
                GitDirectory = @"C:\Git"
            };

            // Act & Assert
            var act = () => SettingsMapper.ToSettings(config);
            act.Should().NotThrow("ToSettings should handle all valid configurations");
        }

        [Fact]
        public void ToSettings_WithNullStrings_HandlesGracefully()
        {
            // Arrange
            var config = new MigrationConfiguration
            {
                VssDirectory = null,
                GitDirectory = null,
                VssProject = null,
                VssExcludePaths = null,
                DefaultEmailDomain = null,
                DefaultComment = null,
                LogFile = null
            };

            // Act
            SettingsMapper.ToSettings(config);

            // Assert
            Settings.Default.VssDirectory.Should().BeNull();
            Settings.Default.GitDirectory.Should().BeNull();
            Settings.Default.VssProject.Should().BeNull();
            Settings.Default.VssExcludePaths.Should().BeNull();
            Settings.Default.DefaultEmailDomain.Should().BeNull();
            Settings.Default.DefaultComment.Should().BeNull();
            Settings.Default.LogFile.Should().BeNull();
        }

        #endregion

        #region Round-Trip Tests

        [Fact]
        public void RoundTrip_ToSettingsAndFromSettings_PreservesAllMappedProperties()
        {
            // Arrange
            var originalConfig = new MigrationConfiguration
            {
                VssDirectory = @"C:\VSS\RoundTrip",
                GitDirectory = @"C:\Git\RoundTrip",
                VssProject = "$/RoundTrip",
                VssExcludePaths = "*.test",
                DefaultEmailDomain = "roundtrip.com",
                DefaultComment = "Round trip",
                LogFile = "roundtrip.log",
                AnyCommentSeconds = 99,
                SameCommentSeconds = 999,
                TranscodeComments = false,
                ForceAnnotatedTags = false,
                ExportProjectToGitRoot = true,
                VssEncoding = Encoding.GetEncoding(1252)
            };

            // Act
            SettingsMapper.ToSettings(originalConfig);
            var resultConfig = SettingsMapper.FromSettings(Encoding.GetEncoding(1252));

            // Assert
            resultConfig.VssDirectory.Should().Be(originalConfig.VssDirectory);
            resultConfig.GitDirectory.Should().Be(originalConfig.GitDirectory);
            resultConfig.VssProject.Should().Be(originalConfig.VssProject);
            resultConfig.VssExcludePaths.Should().Be(originalConfig.VssExcludePaths);
            resultConfig.DefaultEmailDomain.Should().Be(originalConfig.DefaultEmailDomain);
            resultConfig.DefaultComment.Should().Be(originalConfig.DefaultComment);
            resultConfig.LogFile.Should().Be(originalConfig.LogFile);
            resultConfig.AnyCommentSeconds.Should().Be(originalConfig.AnyCommentSeconds);
            resultConfig.SameCommentSeconds.Should().Be(originalConfig.SameCommentSeconds);
            resultConfig.TranscodeComments.Should().Be(originalConfig.TranscodeComments);
            resultConfig.ForceAnnotatedTags.Should().Be(originalConfig.ForceAnnotatedTags);
            resultConfig.ExportProjectToGitRoot.Should().Be(originalConfig.ExportProjectToGitRoot);
            resultConfig.VssEncoding.CodePage.Should().Be(originalConfig.VssEncoding.CodePage);
        }

        [Fact]
        public void RoundTrip_IgnoreErrors_AlwaysResetsToFalse()
        {
            // Arrange
            var config = new MigrationConfiguration
            {
                VssDirectory = @"C:\VSS",
                GitDirectory = @"C:\Git",
                IgnoreErrors = true  // This should not be persisted
            };

            // Act
            SettingsMapper.ToSettings(config);
            var resultConfig = SettingsMapper.FromSettings(Encoding.Default);

            // Assert
            resultConfig.IgnoreErrors.Should().BeFalse(
                "IgnoreErrors is not persisted and should always reset to false");
        }

        #endregion

        #region Integration Tests

        [Fact]
        public void FromSettings_AfterManualSettingsChange_ReflectsNewValues()
        {
            // Arrange
            Settings.Default.VssDirectory = @"C:\Original";
            Settings.Default.GitDirectory = @"C:\Original";

            var config1 = SettingsMapper.FromSettings(Encoding.Default);

            // Act - Manually change settings
            Settings.Default.VssDirectory = @"C:\Modified";
            Settings.Default.GitDirectory = @"C:\Modified";

            var config2 = SettingsMapper.FromSettings(Encoding.Default);

            // Assert
            config1.VssDirectory.Should().Be(@"C:\Original");
            config1.GitDirectory.Should().Be(@"C:\Original");
            config2.VssDirectory.Should().Be(@"C:\Modified");
            config2.GitDirectory.Should().Be(@"C:\Modified");
        }

        #endregion
    }
}
