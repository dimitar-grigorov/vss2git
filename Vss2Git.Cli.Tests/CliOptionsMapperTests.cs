using System;
using System.Text;
using FluentAssertions;
using Hpdi.Vss2Git;
using Xunit;

namespace Hpdi.Vss2Git.Cli.Tests
{
    /// <summary>
    /// Tests for CliOptionsMapper class following the naming convention:
    /// MethodName_Scenario_ExpectedBehavior
    /// </summary>
    public class CliOptionsMapperTests
    {
        static CliOptionsMapperTests()
        {
            // Register code pages encoding provider for Windows-1252, Shift-JIS, etc.
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }
        #region FromOptions Tests

        [Fact]
        public void FromOptions_WithValidOptions_MapsAllProperties()
        {
            // Arrange
            var options = new CliOptions
            {
                VssDirectory = @"C:\VSS\MyRepo",
                GitDirectory = @"C:\Git\MyRepo",
                VssProject = "$/MyProject",
                VssExcludePaths = "*.exe;*.dll",
                DefaultEmailDomain = "example.com",
                DefaultComment = "No comment",
                LogFile = "test.log",
                IgnoreErrors = true,
                AnyCommentSeconds = 60,
                SameCommentSeconds = 300,
                TranscodeComments = false,
                ForceAnnotatedTags = false,
                ExportProjectToGitRoot = true,
                Force = true,              // Should be ignored
                Interactive = true         // Should be ignored
            };
            var encoding = Encoding.UTF8;

            // Act
            var config = CliOptionsMapper.FromOptions(options, encoding);

            // Assert
            config.VssDirectory.Should().Be(@"C:\VSS\MyRepo");
            config.GitDirectory.Should().Be(@"C:\Git\MyRepo");
            config.VssProject.Should().Be("$/MyProject");
            config.VssExcludePaths.Should().Be("*.exe;*.dll");
            config.DefaultEmailDomain.Should().Be("example.com");
            config.DefaultComment.Should().Be("No comment");
            config.LogFile.Should().Be("test.log");
            config.IgnoreErrors.Should().BeTrue();
            config.AnyCommentSeconds.Should().Be(60);
            config.SameCommentSeconds.Should().Be(300);
            config.TranscodeComments.Should().BeFalse();
            config.ForceAnnotatedTags.Should().BeFalse();
            config.ExportProjectToGitRoot.Should().BeTrue();
        }

        [Fact]
        public void FromOptions_WithEncoding_SetsVssEncodingCorrectly()
        {
            // Arrange
            var options = new CliOptions
            {
                VssDirectory = @"C:\VSS",
                GitDirectory = @"C:\Git"
            };
            var encoding = Encoding.GetEncoding(1252); // Windows-1252

            // Act
            var config = CliOptionsMapper.FromOptions(options, encoding);

            // Assert
            config.VssEncoding.Should().BeSameAs(encoding);
            config.VssEncoding.CodePage.Should().Be(1252);
        }

        [Fact]
        public void FromOptions_WithNullEncoding_SetsVssEncodingToNull()
        {
            // Arrange
            var options = new CliOptions
            {
                VssDirectory = @"C:\VSS",
                GitDirectory = @"C:\Git"
            };

            // Act
            var config = CliOptionsMapper.FromOptions(options, null);

            // Assert
            config.VssEncoding.Should().BeNull();
        }

        [Fact]
        public void FromOptions_WithDefaultValues_MapsCorrectly()
        {
            // Arrange
            var options = new CliOptions
            {
                VssDirectory = @"C:\VSS",
                GitDirectory = @"C:\Git",
                VssProject = "$",
                DefaultEmailDomain = "localhost",
                DefaultComment = "",
                AnyCommentSeconds = 30,
                SameCommentSeconds = 600,
                TranscodeComments = true,
                ForceAnnotatedTags = true
            };
            var encoding = Encoding.Default;

            // Act
            var config = CliOptionsMapper.FromOptions(options, encoding);

            // Assert
            config.VssProject.Should().Be("$");
            config.DefaultEmailDomain.Should().Be("localhost");
            config.DefaultComment.Should().Be("");
            config.AnyCommentSeconds.Should().Be(30);
            config.SameCommentSeconds.Should().Be(600);
            config.TranscodeComments.Should().BeTrue();
            config.ForceAnnotatedTags.Should().BeTrue();
        }

        #endregion

        #region ToOptions Tests

        [Fact]
        public void ToOptions_WithValidConfig_MapsAllProperties()
        {
            // Arrange
            var config = new MigrationConfiguration
            {
                VssDirectory = @"C:\VSS\MyRepo",
                GitDirectory = @"C:\Git\MyRepo",
                VssProject = "$/MyProject",
                VssExcludePaths = "*.exe;*.dll",
                DefaultEmailDomain = "example.com",
                DefaultComment = "No comment",
                LogFile = "test.log",
                IgnoreErrors = true,
                AnyCommentSeconds = 60,
                SameCommentSeconds = 300,
                TranscodeComments = false,
                ForceAnnotatedTags = false,
                ExportProjectToGitRoot = true,
                VssEncoding = Encoding.GetEncoding(1252)
            };

            // Act
            var options = CliOptionsMapper.ToOptions(config);

            // Assert
            options.VssDirectory.Should().Be(@"C:\VSS\MyRepo");
            options.GitDirectory.Should().Be(@"C:\Git\MyRepo");
            options.VssProject.Should().Be("$/MyProject");
            options.VssExcludePaths.Should().Be("*.exe;*.dll");
            options.DefaultEmailDomain.Should().Be("example.com");
            options.DefaultComment.Should().Be("No comment");
            options.LogFile.Should().Be("test.log");
            options.IgnoreErrors.Should().BeTrue();
            options.AnyCommentSeconds.Should().Be(60);
            options.SameCommentSeconds.Should().Be(300);
            options.TranscodeComments.Should().BeFalse();
            options.ForceAnnotatedTags.Should().BeFalse();
            options.ExportProjectToGitRoot.Should().BeTrue();
        }

        [Fact]
        public void ToOptions_WithEncoding_SetsEncodingCodePageCorrectly()
        {
            // Arrange
            var config = new MigrationConfiguration
            {
                VssDirectory = @"C:\VSS",
                GitDirectory = @"C:\Git",
                VssEncoding = Encoding.GetEncoding(1252)
            };

            // Act
            var options = CliOptionsMapper.ToOptions(config);

            // Assert
            options.EncodingCodePage.Should().Be(1252);
        }

        [Fact]
        public void ToOptions_WithNullEncoding_LeavesEncodingCodePageNull()
        {
            // Arrange
            var config = new MigrationConfiguration
            {
                VssDirectory = @"C:\VSS",
                GitDirectory = @"C:\Git",
                VssEncoding = null
            };

            // Act
            var options = CliOptionsMapper.ToOptions(config);

            // Assert
            options.EncodingCodePage.Should().BeNull();
        }

        [Fact]
        public void ToOptions_IgnoredProperties_AreNotSet()
        {
            // Arrange
            var config = new MigrationConfiguration
            {
                VssDirectory = @"C:\VSS",
                GitDirectory = @"C:\Git"
            };

            // Act
            var options = CliOptionsMapper.ToOptions(config);

            // Assert - Force and Interactive are not part of MigrationConfiguration
            // so they should have their default values
            options.Force.Should().BeFalse();
            options.Interactive.Should().BeFalse();
        }

        #endregion

        #region Round-Trip Tests

        [Fact]
        public void RoundTrip_FromOptionsToOptions_PreservesAllMappedProperties()
        {
            // Arrange
            var originalOptions = new CliOptions
            {
                VssDirectory = @"C:\VSS\MyRepo",
                GitDirectory = @"C:\Git\MyRepo",
                VssProject = "$/MyProject",
                VssExcludePaths = "*.exe;*.dll",
                DefaultEmailDomain = "example.com",
                DefaultComment = "No comment",
                LogFile = "test.log",
                IgnoreErrors = true,
                AnyCommentSeconds = 60,
                SameCommentSeconds = 300,
                TranscodeComments = false,
                ForceAnnotatedTags = false,
                ExportProjectToGitRoot = true,
                EncodingCodePage = 1252
            };
            var encoding = Encoding.GetEncoding(1252);

            // Act
            var config = CliOptionsMapper.FromOptions(originalOptions, encoding);
            var resultOptions = CliOptionsMapper.ToOptions(config);

            // Assert - all properties except Force and Interactive should be preserved
            resultOptions.VssDirectory.Should().Be(originalOptions.VssDirectory);
            resultOptions.GitDirectory.Should().Be(originalOptions.GitDirectory);
            resultOptions.VssProject.Should().Be(originalOptions.VssProject);
            resultOptions.VssExcludePaths.Should().Be(originalOptions.VssExcludePaths);
            resultOptions.DefaultEmailDomain.Should().Be(originalOptions.DefaultEmailDomain);
            resultOptions.DefaultComment.Should().Be(originalOptions.DefaultComment);
            resultOptions.LogFile.Should().Be(originalOptions.LogFile);
            resultOptions.IgnoreErrors.Should().Be(originalOptions.IgnoreErrors);
            resultOptions.AnyCommentSeconds.Should().Be(originalOptions.AnyCommentSeconds);
            resultOptions.SameCommentSeconds.Should().Be(originalOptions.SameCommentSeconds);
            resultOptions.TranscodeComments.Should().Be(originalOptions.TranscodeComments);
            resultOptions.ForceAnnotatedTags.Should().Be(originalOptions.ForceAnnotatedTags);
            resultOptions.ExportProjectToGitRoot.Should().Be(originalOptions.ExportProjectToGitRoot);
            resultOptions.EncodingCodePage.Should().Be(originalOptions.EncodingCodePage);
        }

        [Fact]
        public void RoundTrip_WithSpecialEncodings_PreservesEncodingCorrectly()
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
                var options = new CliOptions
                {
                    VssDirectory = @"C:\VSS",
                    GitDirectory = @"C:\Git",
                    EncodingCodePage = encoding.CodePage
                };

                // Act
                var config = CliOptionsMapper.FromOptions(options, encoding);
                var resultOptions = CliOptionsMapper.ToOptions(config);

                // Assert
                resultOptions.EncodingCodePage.Should().Be(encoding.CodePage,
                    $"because encoding {encoding.EncodingName} should be preserved");
            }
        }

        #endregion

        #region Date Filtering Tests

        [Fact]
        public void FromOptions_WithFromDateOnly_ParsesCorrectly()
        {
            var options = new CliOptions
            {
                VssDirectory = @"C:\VSS",
                GitDirectory = @"C:\Git",
                FromDate = "2005-01-01"
            };

            var config = CliOptionsMapper.FromOptions(options, Encoding.UTF8);

            config.FromDate.Should().Be(new DateTime(2005, 1, 1));
            config.ToDate.Should().BeNull();
        }

        [Fact]
        public void FromOptions_WithFromDateAndTime_ParsesCorrectly()
        {
            var options = new CliOptions
            {
                VssDirectory = @"C:\VSS",
                GitDirectory = @"C:\Git",
                FromDate = "2005-06-15T14:30:00"
            };

            var config = CliOptionsMapper.FromOptions(options, Encoding.UTF8);

            config.FromDate.Should().Be(new DateTime(2005, 6, 15, 14, 30, 0));
        }

        [Fact]
        public void FromOptions_WithToDateOnly_ParsesCorrectly()
        {
            var options = new CliOptions
            {
                VssDirectory = @"C:\VSS",
                GitDirectory = @"C:\Git",
                ToDate = "2006-12-31"
            };

            var config = CliOptionsMapper.FromOptions(options, Encoding.UTF8);

            config.FromDate.Should().BeNull();
            config.ToDate.Should().Be(new DateTime(2006, 12, 31));
        }

        [Fact]
        public void FromOptions_WithInvalidFromDate_ThrowsArgumentException()
        {
            var options = new CliOptions
            {
                VssDirectory = @"C:\VSS",
                GitDirectory = @"C:\Git",
                FromDate = "not-a-date"
            };

            Action act = () => CliOptionsMapper.FromOptions(options, Encoding.UTF8);

            act.Should().Throw<ArgumentException>()
                .WithMessage("*--from-date*not-a-date*");
        }

        [Fact]
        public void FromOptions_WithInvalidToDate_ThrowsArgumentException()
        {
            var options = new CliOptions
            {
                VssDirectory = @"C:\VSS",
                GitDirectory = @"C:\Git",
                ToDate = "31/12/2006"
            };

            Action act = () => CliOptionsMapper.FromOptions(options, Encoding.UTF8);

            act.Should().Throw<ArgumentException>()
                .WithMessage("*--to-date*31/12/2006*");
        }

        [Fact]
        public void FromOptions_WithNullDates_LeavesConfigDatesNull()
        {
            var options = new CliOptions
            {
                VssDirectory = @"C:\VSS",
                GitDirectory = @"C:\Git",
                FromDate = null,
                ToDate = null
            };

            var config = CliOptionsMapper.FromOptions(options, Encoding.UTF8);

            config.FromDate.Should().BeNull();
            config.ToDate.Should().BeNull();
        }

        [Fact]
        public void ToOptions_WithDates_FormatsCorrectly()
        {
            var config = new MigrationConfiguration
            {
                VssDirectory = @"C:\VSS",
                GitDirectory = @"C:\Git",
                FromDate = new DateTime(2005, 1, 1),
                ToDate = new DateTime(2006, 6, 15, 14, 30, 0)
            };

            var options = CliOptionsMapper.ToOptions(config);

            options.FromDate.Should().Be("2005-01-01T00:00:00");
            options.ToDate.Should().Be("2006-06-15T14:30:00");
        }

        [Fact]
        public void RoundTrip_WithDates_PreservesDates()
        {
            var originalOptions = new CliOptions
            {
                VssDirectory = @"C:\VSS",
                GitDirectory = @"C:\Git",
                FromDate = "2005-03-15T10:00:00",
                ToDate = "2006-12-31T23:59:59"
            };

            var config = CliOptionsMapper.FromOptions(originalOptions, Encoding.UTF8);
            var resultOptions = CliOptionsMapper.ToOptions(config);

            resultOptions.FromDate.Should().Be(originalOptions.FromDate);
            resultOptions.ToDate.Should().Be(originalOptions.ToDate);
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void FromOptions_WithNullStrings_AppliesTargetDefaults()
        {
            // Arrange
            var options = new CliOptions
            {
                VssDirectory = null,
                GitDirectory = null,
                VssProject = null,
                VssExcludePaths = null,
                DefaultEmailDomain = null,
                DefaultComment = null,
                LogFile = null
            };
            var encoding = Encoding.Default;

            // Act
            var config = CliOptionsMapper.FromOptions(options, encoding);

            // Assert - Mapster maps nulls as nulls (doesn't apply target defaults for explicit nulls)
            config.VssDirectory.Should().BeNull();
            config.GitDirectory.Should().BeNull();
            config.VssProject.Should().BeNull();
            config.VssExcludePaths.Should().BeNull();
            config.DefaultEmailDomain.Should().BeNull();
            config.DefaultComment.Should().BeNull();
            config.LogFile.Should().BeNull();
        }

        [Fact]
        public void ToOptions_WithNullStrings_MapsNullCorrectly()
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
            var options = CliOptionsMapper.ToOptions(config);

            // Assert
            options.VssDirectory.Should().BeNull();
            options.GitDirectory.Should().BeNull();
            options.VssProject.Should().BeNull();
            options.VssExcludePaths.Should().BeNull();
            options.DefaultEmailDomain.Should().BeNull();
            options.DefaultComment.Should().BeNull();
            options.LogFile.Should().BeNull();
        }

        #endregion
    }
}
