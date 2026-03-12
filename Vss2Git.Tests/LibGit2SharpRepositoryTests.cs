using System.Text;
using FluentAssertions;

namespace Hpdi.Vss2Git.Tests
{
    /// <summary>
    /// Tests for LibGit2Sharp-specific behavior not covered by GitRepositoryCommonTests.
    /// Common operations (add, remove, move, commit, tag, binary, mixed-state)
    /// are tested in LibGit2SharpCommonTests via the shared base class.
    /// </summary>
    public class LibGit2SharpRepositoryTests : IDisposable
    {
        private readonly string _repoDir;
        private readonly LibGit2SharpRepository _repo;

        public LibGit2SharpRepositoryTests()
        {
            _repoDir = Path.Combine(Path.GetTempPath(),
                "vss2git_l2s_test_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(_repoDir);
            _repo = new LibGit2SharpRepository(_repoDir, Logger.Null);
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

        #region L3: CommitEncoding and i18n.commitencoding

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
                "i18n.commitencoding should not be set to non-UTF-8 for LibGit2Sharp backend");
        }

        #endregion
    }
}
