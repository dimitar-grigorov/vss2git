using System.Diagnostics;
using System.Text;

namespace Hpdi.Vss2Git.TestDbBuilder;

/// <summary>
/// Wraps ss.exe and mkss.exe with strict error handling.
/// All commands use -YAdmin for identity and -I-Y for non-interactive mode.
/// </summary>
public class VssCommandRunner : IDisposable
{
    private readonly string ssExePath;
    private readonly string mkssExePath;
    private readonly string databasePath;
    private readonly string workingDirectory;
    private bool disposed;

    public string DatabasePath => databasePath;
    public string WorkingDirectory => workingDirectory;

    public VssCommandRunner(string vssInstallDir, string databasePath, string workingDirectory)
    {
        ssExePath = Path.Combine(vssInstallDir, "ss.exe");
        mkssExePath = Path.Combine(vssInstallDir, "mkss.exe");
        this.databasePath = Path.GetFullPath(databasePath);
        this.workingDirectory = Path.GetFullPath(workingDirectory);

        if (!File.Exists(ssExePath))
            throw new FileNotFoundException($"ss.exe not found at: {ssExePath}");
        if (!File.Exists(mkssExePath))
            throw new FileNotFoundException($"mkss.exe not found at: {mkssExePath}");
    }

    #region Database Management

    public void CreateDatabase()
    {
        if (Directory.Exists(databasePath))
            Directory.Delete(databasePath, true);

        Directory.CreateDirectory(databasePath);

        var (exitCode, _, stderr) = RunProcess(mkssExePath, $"\"{databasePath}\"", null);
        if (exitCode != 0)
            throw new VssCommandException("mkss", $"Failed to create database: {stderr}");

        var iniPath = Path.Combine(databasePath, "srcsafe.ini");
        if (!File.Exists(iniPath))
            throw new VssCommandException("mkss", $"srcsafe.ini not found at: {iniPath}");

        // Disable Checkout_LocalVer_Default in all user ss.ini files.
        // VSS 6.0d creates vssver2.scc files that corrupt checkout state tracking.
        var usersDir = Path.Combine(databasePath, "users");
        if (Directory.Exists(usersDir))
        {
            foreach (var userDir in Directory.GetDirectories(usersDir))
            {
                var userIni = Path.Combine(userDir, "ss.ini");
                if (File.Exists(userIni))
                {
                    var content = File.ReadAllText(userIni);
                    if (content.Contains("Checkout_LocalVer_Default"))
                    {
                        content = content.Replace("Checkout_LocalVer_Default  = Yes",
                            "Checkout_LocalVer_Default  = No");
                        File.WriteAllText(userIni, content);
                    }
                }
            }
        }

        Console.WriteLine($"  Created VSS database at: {databasePath}");
    }

    #endregion

    #region Project Operations

    public void CreateProject(string projectPath, string? comment = null)
    {
        var args = $"Create \"{projectPath}\" -I-Y";
        args += comment != null ? $" -C\"{comment}\"" : " -C-";
        RunSs(args, "Create");
        Console.WriteLine($"  Created project: {projectPath}");
    }

    public void SetCurrentProject(string projectPath)
    {
        RunSs($"CP \"{projectPath}\" -I-Y", "CP");
    }

    public void Move(string projectPath, string newParentPath)
    {
        RunSs($"Move \"{projectPath}\" \"{newParentPath}\" -I-Y", "Move");
        Console.WriteLine($"  Moved: {projectPath} -> {newParentPath}");
    }

    public void Cloak(string projectPath)
    {
        RunSs($"Cloak \"{projectPath}\" -I-Y", "Cloak");
        Console.WriteLine($"  Cloaked: {projectPath}");
    }

    public void Decloak(string projectPath)
    {
        RunSs($"Decloak \"{projectPath}\" -I-Y", "Decloak");
        Console.WriteLine($"  Decloaked: {projectPath}");
    }

    #endregion

    #region File Operations

    public void AddFile(string filePath, string? comment = null)
    {
        var args = $"Add \"{filePath}\" -I-Y";
        args += comment != null ? $" -C\"{comment}\"" : " -C-";
        RunSs(args, "Add");
        Console.WriteLine($"  Added file: {filePath}");
    }

    public void Checkout(string itemPath)
    {
        RunSs($"Checkout \"{itemPath}\" -I-Y", "Checkout");
    }

    public void UndoCheckout(string itemPath)
    {
        RunSs($"Undocheckout \"{itemPath}\" -I-Y -G-", "Undocheckout");
    }

    public void Checkin(string itemPath, string? comment = null)
    {
        var args = $"Checkin \"{itemPath}\" -I-Y";
        args += comment != null ? $" -C\"{comment}\"" : " -C-";
        RunSs(args, "Checkin");
        Console.WriteLine($"  Checked in: {itemPath}");
    }

    public void Get(string itemPath, string? outputDir = null)
    {
        var args = $"Get \"{itemPath}\" -I-Y -W";
        if (outputDir != null)
            args += $" -GL\"{outputDir}\"";
        RunSs(args, "Get");
    }

    public void Filetype(string itemPath, bool binary)
    {
        RunSs($"Filetype \"{itemPath}\" {(binary ? "-B" : "-T")} -I-Y", "Filetype");
        Console.WriteLine($"  Set filetype: {itemPath} -> {(binary ? "binary" : "text")}");
    }

    #endregion

    #region Item Management (Delete, Recover, Destroy, Rename)

    public void Delete(string itemPath)
    {
        RunSs($"Delete \"{itemPath}\" -I-Y", "Delete");
        Console.WriteLine($"  Deleted: {itemPath}");
    }

    public void Recover(string itemPath)
    {
        RunSs($"Recover \"{itemPath}\" -I-Y -G-", "Recover");
        Console.WriteLine($"  Recovered: {itemPath}");
    }

    public void Destroy(string itemPath)
    {
        RunSs($"Destroy \"{itemPath}\" -I-Y", "Destroy");
        Console.WriteLine($"  Destroyed: {itemPath}");
    }

    /// <summary>
    /// Purge permanently removes already-deleted items (frees space).
    /// </summary>
    public void Purge(string itemPath)
    {
        RunSs($"Purge \"{itemPath}\" -I-Y", "Purge");
        Console.WriteLine($"  Purged: {itemPath}");
    }

    /// <summary>
    /// Rename requires setting current project first (VSS quirk).
    /// </summary>
    public void Rename(string itemPath, string newName)
    {
        var lastSlash = itemPath.LastIndexOf('/');
        if (lastSlash > 0)
            SetCurrentProject(itemPath.Substring(0, lastSlash));

        RunSs($"Rename \"{itemPath}\" \"{newName}\" -I-Y", "Rename");
        Console.WriteLine($"  Renamed: {itemPath} -> {newName}");
    }

    #endregion

    #region Sharing and Branching

    /// <summary>
    /// Share tolerates "writable copy" warnings (exit code 100 with "Sharing" in stdout).
    /// </summary>
    public void Share(string sourceItemPath, string targetProjectPath, string? comment = null)
    {
        SetCurrentProject(targetProjectPath);
        var args = $"Share \"{sourceItemPath}\" -I-Y";
        args += comment != null ? $" -C\"{comment}\"" : " -C-";

        var fullArgs = args + " -YAdmin";
        var result = RunProcess(ssExePath, fullArgs, databasePath);
        if (result.exitCode != 0 &&
            !result.stderr.Contains("writable copy", StringComparison.OrdinalIgnoreCase) &&
            !result.stdout.Contains("Sharing", StringComparison.OrdinalIgnoreCase))
        {
            throw new VssCommandException("Share",
                $"ss.exe failed (exit code {result.exitCode})\n" +
                $"  Args: {fullArgs}\n" +
                $"  Stdout: {result.stdout}\n" +
                $"  Stderr: {result.stderr}");
        }
        Console.WriteLine($"  Shared: {sourceItemPath} -> {targetProjectPath}");
    }

    public void Branch(string itemPath)
    {
        RunSs($"Branch \"{itemPath}\" -I-Y -G-", "Branch");
        Console.WriteLine($"  Branched: {itemPath}");
    }

    #endregion

    #region Versioning (Pin, Unpin, Label, Rollback)

    public void Pin(string itemPath, int version)
    {
        RunSs($"Pin \"{itemPath}\" -V{version} -I-Y -G-", "Pin");
        Console.WriteLine($"  Pinned: {itemPath} at version {version}");
    }

    public void Unpin(string itemPath)
    {
        RunSs($"Unpin \"{itemPath}\" -I-Y -G-", "Unpin");
        Console.WriteLine($"  Unpinned: {itemPath}");
    }

    public void Label(string projectPath, string label, string? comment = null)
    {
        var args = $"Label \"{projectPath}\" -L\"{label}\" -I-Y";
        args += comment != null ? $" -C\"{comment}\"" : " -C-";
        RunSs(args, "Label");
        Console.WriteLine($"  Labeled: {projectPath} as '{label}'");
    }

    /// <summary>
    /// Roll back a file to a previous version (destroys later versions).
    /// </summary>
    public void Rollback(string itemPath, int version)
    {
        RunSs($"Rollback \"{itemPath}\" -V{version} -I-Y", "Rollback");
        Console.WriteLine($"  Rolled back: {itemPath} to version {version}");
    }

    #endregion

    #region Query Commands

    public string History(string itemPath)
    {
        var (_, stdout, _) = RunSs($"History \"{itemPath}\" -I-Y", "History");
        return stdout;
    }

    public string Status(string itemPath)
    {
        var (_, stdout, _) = RunSs($"Status \"{itemPath}\" -I-Y", "Status");
        return stdout;
    }

    public string Dir(string projectPath, bool recursive = false)
    {
        var args = $"Dir \"{projectPath}\" -I-Y";
        if (recursive)
            args += " -R";
        var (_, stdout, _) = RunSs(args, "Dir");
        return stdout;
    }

    public string View(string itemPath, int? version = null)
    {
        var args = $"View \"{itemPath}\" -I-Y";
        if (version.HasValue)
            args += $" -V{version.Value}";
        var (_, stdout, _) = RunSs(args, "View");
        return stdout;
    }

    public string Links(string itemPath)
    {
        var (_, stdout, _) = RunSs($"Links \"{itemPath}\" -I-Y", "Links");
        return stdout;
    }

    public string Properties(string itemPath)
    {
        var (_, stdout, _) = RunSs($"Properties \"{itemPath}\" -I-Y", "Properties");
        return stdout;
    }

    public void Comment(string itemPath, string newComment, int? version = null)
    {
        var args = version.HasValue
            ? $"Comment \"{itemPath}\" -V{version.Value} -C\"{newComment}\" -I-Y"
            : $"Comment \"{itemPath}\" -C\"{newComment}\" -I-Y";
        RunSs(args, "Comment");
        Console.WriteLine($"  Comment updated: {itemPath}");
    }

    #endregion

    #region Convenience Methods

    /// <summary>
    /// Create a file on disk and add it to VSS.
    /// Sets file read-only after Add to prevent "writable copy" errors on later Checkout.
    /// Only needs CP (not Workfold) since ss Add takes a full disk path.
    /// </summary>
    public void CreateAndAddFile(string projectPath, string fileName, string content, string? comment = null)
    {
        SetCurrentProject(projectPath);

        var projectWorkDir = GetProjectWorkDir(projectPath);
        Directory.CreateDirectory(projectWorkDir);

        var filePath = Path.Combine(projectWorkDir, fileName);
        File.WriteAllText(filePath, content, Encoding.UTF8);

        AddFile(filePath, comment);

        File.SetAttributes(filePath, File.GetAttributes(filePath) | FileAttributes.ReadOnly);
    }

    /// <summary>
    /// Checkout, modify, checkin. Uses -GL to force correct working directory
    /// (VSS working folder mappings are unreliable across process invocations).
    /// Only needs CP (not Workfold) since -GL overrides the working folder.
    /// </summary>
    public void EditFile(string projectPath, string fileName, string newContent, string? comment = null)
    {
        var vssPath = $"{projectPath}/{fileName}";
        var projectWorkDir = GetProjectWorkDir(projectPath);
        Directory.CreateDirectory(projectWorkDir);
        SetCurrentProject(projectPath);

        RunSs($"Checkout \"{vssPath}\" -I-Y -GL\"{projectWorkDir}\"", "Checkout for edit");

        var filePath = Path.Combine(projectWorkDir, fileName);
        File.WriteAllText(filePath, newContent, Encoding.UTF8);

        RunSs($"Checkin \"{vssPath}\" -I-Y -GL\"{projectWorkDir}\"" +
            (comment != null ? $" -C\"{comment}\"" : " -C-"), "Checkin after edit");
        Console.WriteLine($"  Checked in: {vssPath}");
    }

    public void SetWorkingFolder(string projectPath)
    {
        var projectWorkDir = GetProjectWorkDir(projectPath);
        Directory.CreateDirectory(projectWorkDir);
        RunSs($"Workfold \"{projectPath}\" \"{projectWorkDir}\" -I-Y", "Workfold");
    }

    /// <summary>
    /// Convert VSS path $/Foo/Bar to local path workingDirectory\Foo\Bar
    /// </summary>
    public string GetProjectWorkDir(string projectPath)
    {
        var relativePath = projectPath.TrimStart('$', '/').Replace('/', Path.DirectorySeparatorChar);
        return string.IsNullOrEmpty(relativePath) ? workingDirectory : Path.Combine(workingDirectory, relativePath);
    }

    #endregion

    #region Infrastructure

    public static void ClearReadOnly(string filePath)
    {
        if (File.Exists(filePath))
        {
            var attrs = File.GetAttributes(filePath);
            if ((attrs & FileAttributes.ReadOnly) != 0)
                File.SetAttributes(filePath, attrs & ~FileAttributes.ReadOnly);
        }
    }

    private (int exitCode, string stdout, string stderr) RunSs(string arguments, string operationName)
    {
        var fullArgs = arguments + " -YAdmin";
        var result = RunProcess(ssExePath, fullArgs, databasePath);
        if (result.exitCode != 0)
        {
            throw new VssCommandException(operationName,
                $"ss.exe failed (exit code {result.exitCode})\n" +
                $"  Args: {fullArgs}\n" +
                $"  Stdout: {result.stdout}\n" +
                $"  Stderr: {result.stderr}");
        }
        return result;
    }

    private (int exitCode, string stdout, string stderr) RunProcess(string exePath, string arguments, string? ssDir)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        if (ssDir != null)
            psi.EnvironmentVariables["SSDIR"] = ssDir;
        psi.EnvironmentVariables["SSUSER"] = "Admin";

        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit(30000);

        if (!process.HasExited)
        {
            process.Kill();
            throw new VssCommandException("Timeout",
                $"Process did not exit within 30 seconds: {exePath} {arguments}");
        }

        return (process.ExitCode, stdout.Trim(), stderr.Trim());
    }

    public void Dispose()
    {
        if (!disposed)
            disposed = true;
    }

    #endregion
}

public class VssCommandException : Exception
{
    public string Operation { get; }

    public VssCommandException(string operation, string message)
        : base($"[{operation}] {message}")
    {
        Operation = operation;
    }
}
