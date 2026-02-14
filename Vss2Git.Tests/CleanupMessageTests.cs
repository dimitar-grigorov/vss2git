using FluentAssertions;
using Xunit;

namespace Hpdi.Vss2Git.Tests
{
    /// <summary>
    /// Tests for LibGit2SharpRepository.CleanupMessage which normalizes
    /// commit/tag messages to match git's default 'strip' cleanup mode.
    ///
    /// Bugs fixed by CleanupMessage:
    /// - Commit hash divergence at commit #6: missing trailing newline
    /// - Commit hash divergence at commit #22: CRLF not converted to LF
    /// - Commit hash divergence at commit #214: trailing whitespace not stripped
    /// </summary>
    public class CleanupMessageTests
    {
        #region Null and empty inputs

        [Fact]
        public void CleanupMessage_Null_ReturnsEmpty()
        {
            LibGit2SharpRepository.CleanupMessage(null).Should().Be("");
        }

        [Fact]
        public void CleanupMessage_EmptyString_ReturnsEmpty()
        {
            LibGit2SharpRepository.CleanupMessage("").Should().Be("");
        }

        [Fact]
        public void CleanupMessage_OnlyWhitespace_ReturnsEmpty()
        {
            LibGit2SharpRepository.CleanupMessage("   \t  ").Should().Be("");
        }

        [Fact]
        public void CleanupMessage_OnlyBlankLines_ReturnsEmpty()
        {
            LibGit2SharpRepository.CleanupMessage("\n\n\n").Should().Be("");
        }

        #endregion

        #region Trailing newline (fixed commit #6 divergence)

        [Fact]
        public void CleanupMessage_SingleLine_AddsTrailingNewline()
        {
            // Bug: ObjectDatabase.CreateCommit() doesn't append trailing newline
            // like 'git commit' does. This caused commit hash divergence at commit #6.
            LibGit2SharpRepository.CleanupMessage("Initial commit")
                .Should().Be("Initial commit\n");
        }

        [Fact]
        public void CleanupMessage_SingleLineWithExistingNewline_PreservesExactlyOneNewline()
        {
            LibGit2SharpRepository.CleanupMessage("Initial commit\n")
                .Should().Be("Initial commit\n");
        }

        [Fact]
        public void CleanupMessage_MultipleTrailingNewlines_CollapsesToOne()
        {
            LibGit2SharpRepository.CleanupMessage("Initial commit\n\n\n")
                .Should().Be("Initial commit\n");
        }

        #endregion

        #region CRLF normalization (fixed commit #22 divergence)

        [Fact]
        public void CleanupMessage_CrLf_ConvertedToLf()
        {
            // Bug: VSS comments have Windows line endings (\r\n) but git normalizes
            // to \n. This caused commit hash divergence at commit #22.
            LibGit2SharpRepository.CleanupMessage("line1\r\nline2\r\nline3")
                .Should().Be("line1\nline2\nline3\n");
        }

        [Fact]
        public void CleanupMessage_LoneCr_ConvertedToLf()
        {
            LibGit2SharpRepository.CleanupMessage("line1\rline2")
                .Should().Be("line1\nline2\n");
        }

        [Fact]
        public void CleanupMessage_MixedLineEndings_AllNormalizedToLf()
        {
            LibGit2SharpRepository.CleanupMessage("line1\r\nline2\rline3\nline4")
                .Should().Be("line1\nline2\nline3\nline4\n");
        }

        #endregion

        #region Trailing whitespace stripping (fixed commit #214 divergence)

        [Fact]
        public void CleanupMessage_TrailingSpaces_Stripped()
        {
            // Bug: git's default 'strip' cleanup mode removes trailing whitespace
            // per line. This caused commit hash divergence at commit #214.
            LibGit2SharpRepository.CleanupMessage("line with trailing spaces   ")
                .Should().Be("line with trailing spaces\n");
        }

        [Fact]
        public void CleanupMessage_TrailingTabs_Stripped()
        {
            LibGit2SharpRepository.CleanupMessage("line with tabs\t\t")
                .Should().Be("line with tabs\n");
        }

        [Fact]
        public void CleanupMessage_MultiLineTrailingWhitespace_StrippedPerLine()
        {
            LibGit2SharpRepository.CleanupMessage("first line  \nsecond line\t\nthird line ")
                .Should().Be("first line\nsecond line\nthird line\n");
        }

        #endregion

        #region Leading blank lines

        [Fact]
        public void CleanupMessage_LeadingBlankLines_Stripped()
        {
            LibGit2SharpRepository.CleanupMessage("\n\nActual message")
                .Should().Be("Actual message\n");
        }

        [Fact]
        public void CleanupMessage_LeadingWhitespaceOnlyLines_Stripped()
        {
            // Lines that are only whitespace become blank after TrimEnd
            LibGit2SharpRepository.CleanupMessage("   \n  \nActual message")
                .Should().Be("Actual message\n");
        }

        #endregion

        #region Combined scenarios (realistic VSS comments)

        [Fact]
        public void CleanupMessage_TypicalVssComment_NormalizedCorrectly()
        {
            // Realistic VSS comment with CRLF, trailing spaces, and blank lines
            var vssComment = "\r\nFixed bug in data importer  \r\n\r\nThe parser was not handling\r\nnull values correctly.  \r\n\r\n";
            LibGit2SharpRepository.CleanupMessage(vssComment)
                .Should().Be("Fixed bug in data importer\n\nThe parser was not handling\nnull values correctly.\n");
        }

        [Fact]
        public void CleanupMessage_InternalBlankLines_Preserved()
        {
            // Blank lines between paragraphs should be preserved
            LibGit2SharpRepository.CleanupMessage("Paragraph 1\n\nParagraph 2")
                .Should().Be("Paragraph 1\n\nParagraph 2\n");
        }

        [Fact]
        public void CleanupMessage_LeadingWhitespaceOnContentLines_Preserved()
        {
            // Leading whitespace (indentation) is NOT stripped - only trailing
            LibGit2SharpRepository.CleanupMessage("  indented line")
                .Should().Be("  indented line\n");
        }

        #endregion
    }
}
