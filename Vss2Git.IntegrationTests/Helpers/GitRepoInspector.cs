using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Hpdi.Vss2Git.IntegrationTests.Helpers;

/// <summary>
/// Inspects a Git repository for test assertions.
/// Wraps git commands to query commits, tags, files, and content.
/// </summary>
public class GitRepoInspector
{
    private readonly string _repoPath;

    public GitRepoInspector(string repoPath)
    {
        _repoPath = repoPath;
    }

    public int GetCommitCount()
    {
        var output = RunGit("rev-list --count HEAD").Trim();
        return int.Parse(output);
    }

    public List<GitCommitInfo> GetCommits()
    {
        // Format: hash|author|email|date|subject
        var output = RunGit("log --format=\"%H|%an|%ae|%ai|%s\" --reverse");
        var commits = new List<GitCommitInfo>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('|', 5);
            if (parts.Length >= 5)
            {
                commits.Add(new GitCommitInfo
                {
                    Hash = parts[0],
                    Author = parts[1],
                    Email = parts[2],
                    Date = parts[3],
                    Subject = parts[4]
                });
            }
        }
        return commits;
    }

    public List<string> GetFileList()
    {
        var output = RunGit("ls-tree -r --name-only HEAD");
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(f => f.Trim())
            .ToList();
    }

    public string GetFileContent(string path)
    {
        return RunGit($"show HEAD:\"{path}\"");
    }

    public List<string> GetTags()
    {
        var output = RunGit("tag --list");
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .ToList();
    }

    public string GetTagMessage(string tagName)
    {
        return RunGit($"tag -n99 \"{tagName}\"").Trim();
    }

    public bool FileExists(string path)
    {
        var files = GetFileList();
        return files.Any(f => f.Equals(path, StringComparison.OrdinalIgnoreCase));
    }

    public bool DirectoryExists(string path)
    {
        var normalizedPath = path.TrimEnd('/') + "/";
        var files = GetFileList();
        return files.Any(f => f.StartsWith(normalizedPath, StringComparison.OrdinalIgnoreCase));
    }

    public List<string> GetFilesInDirectory(string dirPath)
    {
        var normalizedPath = dirPath.TrimEnd('/') + "/";
        return GetFileList()
            .Where(f => f.StartsWith(normalizedPath, StringComparison.OrdinalIgnoreCase))
            .Select(f => f.Substring(normalizedPath.Length))
            .ToList();
    }

    private string RunGit(string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = _repoPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit(30_000);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"git {arguments} failed (exit {process.ExitCode}):\n{error}");
        }

        return output;
    }
}

public class GitCommitInfo
{
    public string Hash { get; set; } = "";
    public string Author { get; set; } = "";
    public string Email { get; set; } = "";
    public string Date { get; set; } = "";
    public string Subject { get; set; } = "";
}
