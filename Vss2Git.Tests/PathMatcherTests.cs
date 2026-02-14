using FluentAssertions;
using Xunit;

namespace Hpdi.Vss2Git.Tests
{
    /// <summary>
    /// Tests for PathMatcher glob-to-regex conversion.
    /// C2 bug: missing start anchor allows substring matches
    /// (e.g., "bin/*" matches "$/mybin/file.obj").
    /// </summary>
    public class PathMatcherTests
    {
        #region C2: Substring matching bug

        [Fact]
        public void WildcardExtension_MatchesFileAnywhere()
        {
            var matcher = new PathMatcher("*.exe");

            matcher.Matches("$/MyProject/bin/app.exe").Should().BeTrue();
            matcher.Matches("$/dir/sub/file.exe").Should().BeTrue();
            matcher.Matches("app.exe").Should().BeTrue();
        }

        [Fact]
        public void WildcardExtension_DoesNotMatchDifferentExtension()
        {
            var matcher = new PathMatcher("*.exe");

            matcher.Matches("$/dir/file.txt").Should().BeFalse();
            matcher.Matches("$/dir/file.executive").Should().BeFalse();
        }

        [Fact]
        public void DirectoryPattern_MatchesExactSegment()
        {
            var matcher = new PathMatcher("bin/*");

            // Should match "bin" as a complete path segment
            matcher.Matches("$/src/bin/file.obj").Should().BeTrue();
            matcher.Matches("bin/file.obj").Should().BeTrue();
        }

        [Fact]
        public void DirectoryPattern_DoesNotMatchSubstring()
        {
            // C2 BUG: without start anchor, "bin/*" matches "mybin/file"
            // because regex finds "bin/" as a substring inside "mybin/"
            var matcher = new PathMatcher("bin/*");

            matcher.Matches("$/mybin/file.obj").Should().BeFalse(
                "bin/* should match 'bin' as a complete segment, not substring of 'mybin'");
            matcher.Matches("$/recbin/file.obj").Should().BeFalse();
        }

        [Fact]
        public void DirectoryPattern_WithSubdirs_DoesNotMatchSubstring()
        {
            var matcher = new PathMatcher("src/bin/*");

            matcher.Matches("$/proj/src/bin/file.obj").Should().BeTrue();
            matcher.Matches("$/proj/mysrc/bin/file.obj").Should().BeFalse(
                "'src' should match as a complete segment");
        }

        #endregion

        #region General glob functionality

        [Fact]
        public void DoubleStarWildcard_MatchesAnyPath()
        {
            var matcher = new PathMatcher("**/bin/*");

            matcher.Matches("$/proj/src/bin/file.obj").Should().BeTrue();
            matcher.Matches("$/bin/file.obj").Should().BeTrue();
        }

        [Fact]
        public void QuestionMark_MatchesSingleChar()
        {
            var matcher = new PathMatcher("file?.txt");

            matcher.Matches("$/dir/file1.txt").Should().BeTrue();
            matcher.Matches("$/dir/fileA.txt").Should().BeTrue();
            matcher.Matches("$/dir/file.txt").Should().BeFalse("? requires exactly one char");
            matcher.Matches("$/dir/file12.txt").Should().BeFalse("? matches only one char");
        }

        [Fact]
        public void CaseInsensitive_MatchesRegardlessOfCase()
        {
            var matcher = new PathMatcher("*.EXE");

            matcher.Matches("$/dir/file.exe").Should().BeTrue();
            matcher.Matches("$/dir/FILE.EXE").Should().BeTrue();
            matcher.Matches("$/dir/File.Exe").Should().BeTrue();
        }

        [Fact]
        public void MultiplePatterns_MatchesAny()
        {
            var matcher = new PathMatcher(new[] { "*.exe", "*.dll" });

            matcher.Matches("$/dir/app.exe").Should().BeTrue();
            matcher.Matches("$/dir/lib.dll").Should().BeTrue();
            matcher.Matches("$/dir/file.txt").Should().BeFalse();
        }

        [Fact]
        public void MultiplePatterns_AllRespectSegmentBoundary()
        {
            // Both patterns in a multi-pattern matcher should respect segment boundaries
            var matcher = new PathMatcher(new[] { "bin/*", "obj/*" });

            matcher.Matches("$/proj/bin/file.obj").Should().BeTrue();
            matcher.Matches("$/proj/obj/file.dll").Should().BeTrue();
            matcher.Matches("$/proj/mybin/file.obj").Should().BeFalse();
            matcher.Matches("$/proj/myobj/file.dll").Should().BeFalse();
        }

        [Fact]
        public void LiteralFilename_MatchesExactName()
        {
            var matcher = new PathMatcher("Thumbs.db");

            matcher.Matches("$/proj/Thumbs.db").Should().BeTrue();
            matcher.Matches("$/proj/sub/Thumbs.db").Should().BeTrue();
            matcher.Matches("$/proj/NotThumbs.db").Should().BeFalse(
                "should not match as substring");
        }

        [Fact]
        public void VssRootPattern_MatchesFromStart()
        {
            var matcher = new PathMatcher("$/Obsolete/**");

            matcher.Matches("$/Obsolete/file.txt").Should().BeTrue();
            matcher.Matches("$/Obsolete/sub/file.txt").Should().BeTrue();
            matcher.Matches("$/Other/Obsolete/file.txt").Should().BeFalse();
        }

        [Fact]
        public void PathSeparators_AcceptsBothStyles()
        {
            var matcher = new PathMatcher("bin/*");

            matcher.Matches("$/proj/bin/file.obj").Should().BeTrue();
            matcher.Matches("$/proj\\bin\\file.obj").Should().BeTrue();
        }

        #endregion
    }
}
