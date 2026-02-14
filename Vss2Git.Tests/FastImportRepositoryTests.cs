using System.Text;
using FluentAssertions;
using Xunit;

namespace Hpdi.Vss2Git.Tests
{
    /// <summary>
    /// Tests for FastImport-specific behavior not covered by GitRepositoryCommonTests.
    /// Common operations (add, remove, move, commit, tag, binary) are tested
    /// in FastImportCommonTests via the shared base class.
    /// </summary>
    public class FastImportRepositoryTests : IDisposable
    {
        private readonly string _repoDir;
        private readonly FastImportRepository _repo;

        public FastImportRepositoryTests()
        {
            _repoDir = Path.Combine(Path.GetTempPath(),
                "vss2git_fi_test_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(_repoDir);
            _repo = new FastImportRepository(_repoDir, Logger.Null);
            _repo.Init();
        }

        public void Dispose()
        {
            _repo.Dispose();
            try
            {
                if (Directory.Exists(_repoDir))
                {
                    foreach (var f in Directory.EnumerateFiles(_repoDir, "*", SearchOption.AllDirectories))
                        File.SetAttributes(f, FileAttributes.Normal);
                    Directory.Delete(_repoDir, true);
                }
            }
            catch { }
        }

        private string CreateFile(string relativePath, string content = "test content")
        {
            var fullPath = Path.Combine(_repoDir, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, content);
            return fullPath;
        }

        #region Encoding: i18n.commitencoding skip (shared with LibGit2Sharp)

        [Fact]
        public void SetConfig_SkipsCommitEncoding_SoGitLogShowsUtf8()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var cp1251 = Encoding.GetEncoding(1251);
            _repo.CommitEncoding = cp1251;
            _repo.SetConfig("i18n.commitencoding", cp1251.WebName);

            var f1 = CreateFile("test.txt", "content");
            _repo.AddAll(new[] { f1 });
            _repo.Commit("Иван Петров", "ivan@test.com", "Промяна на файл", DateTime.Now);

            _repo.Dispose();
            var psi = new System.Diagnostics.ProcessStartInfo("git", "log --format=%s -1")
            {
                WorkingDirectory = _repoDir,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                StandardOutputEncoding = Encoding.UTF8
            };
            using var proc = System.Diagnostics.Process.Start(psi)!;
            var subject = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit();

            subject.Should().Be("Промяна на файл",
                "i18n.commitencoding should not be set to non-UTF-8 for FastImport backend");
        }

        #endregion

        #region Tags: edge case

        [Fact]
        public void Tag_BeforeFirstCommit_DoesNotThrow()
        {
            // Should log a warning but not crash
            var act = () => _repo.Tag("v0.0", "test", "test@test", "early tag", DateTime.Now);
            act.Should().NotThrow();
        }

        #endregion

        #region Path quoting (FastImport-specific API)

        [Fact]
        public void QuotePath_PlainPath_NotQuoted()
        {
            FastImportRepository.QuotePath("src/main.c").Should().Be("src/main.c");
        }

        [Fact]
        public void QuotePath_PathWithSpaces_IsQuoted()
        {
            FastImportRepository.QuotePath("my project/file name.txt")
                .Should().Be("\"my project/file name.txt\"");
        }

        [Fact]
        public void QuotePath_PathWithQuotes_IsEscaped()
        {
            FastImportRepository.QuotePath("file\"name.txt")
                .Should().Be("\"file\\\"name.txt\"");
        }

        #endregion
    }
}
