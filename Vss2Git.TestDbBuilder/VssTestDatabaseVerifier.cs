using System.Text;
using Hpdi.VssLogicalLib;

namespace Hpdi.Vss2Git.TestDbBuilder;

/// <summary>
/// Verifies VSS databases using VssLogicalLib after creation.
/// </summary>
public class VssTestDatabaseVerifier
{
    private readonly string databasePath;

    public VssTestDatabaseVerifier(string databasePath)
    {
        this.databasePath = databasePath;
    }

    public VssDatabase OpenDatabase()
    {
        var factory = new VssDatabaseFactory(databasePath);
        factory.Encoding = Encoding.Default;
        return factory.Open();
    }

    public void VerifyProjectExists(VssDatabase db, string projectPath)
    {
        var item = db.GetItem(projectPath);
        if (item == null)
            throw new VerificationException($"Project not found: {projectPath}");
        if (!item.IsProject)
            throw new VerificationException($"Item at {projectPath} is not a project");

        Console.WriteLine($"  [OK] Project exists: {projectPath}");
    }

    public void VerifyFileExists(VssDatabase db, string projectPath, string fileName)
    {
        var item = db.GetItem(projectPath);
        if (item == null || !item.IsProject)
            throw new VerificationException($"Project not found: {projectPath}");

        var project = (VssProject)item;
        var file = project.FindFile(fileName);
        if (file == null)
            throw new VerificationException($"File not found: {fileName} in {projectPath}");

        Console.WriteLine($"  [OK] File exists: {projectPath}/{fileName}");
    }

    public void VerifyFileRevisionCount(VssDatabase db, string projectPath, string fileName, int expectedCount)
    {
        var project = (VssProject)db.GetItem(projectPath);
        var file = project.FindFile(fileName);
        if (file == null)
            throw new VerificationException($"File not found: {fileName} in {projectPath}");

        var actualCount = file.RevisionCount;
        if (actualCount != expectedCount)
            throw new VerificationException(
                $"File {projectPath}/{fileName}: expected {expectedCount} revisions, found {actualCount}");

        Console.WriteLine($"  [OK] File {projectPath}/{fileName} has {actualCount} revisions");
    }

    public void VerifyFileContent(VssDatabase db, string projectPath, string fileName,
        int version, string expectedContent)
    {
        var project = (VssProject)db.GetItem(projectPath);
        var file = project.FindFile(fileName);
        if (file == null)
            throw new VerificationException($"File not found: {fileName} in {projectPath}");

        var revision = (VssFileRevision)file.GetRevision(version);
        using var stream = revision.GetContents();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var actualContent = reader.ReadToEnd();

        if (actualContent != expectedContent)
            throw new VerificationException(
                $"File {projectPath}/{fileName} v{version}: content mismatch\n" +
                $"  Expected: [{expectedContent.Length} chars] {Truncate(expectedContent, 80)}\n" +
                $"  Actual:   [{actualContent.Length} chars] {Truncate(actualContent, 80)}");

        Console.WriteLine($"  [OK] File {projectPath}/{fileName} v{version} content matches");
    }

    public void VerifyProjectCount(VssDatabase db, string projectPath, int expectedCount)
    {
        var project = (VssProject)db.GetItem(projectPath);
        var actualCount = project.Projects.Count();
        if (actualCount != expectedCount)
            throw new VerificationException(
                $"Project {projectPath}: expected {expectedCount} sub-projects, found {actualCount}");

        Console.WriteLine($"  [OK] Project {projectPath} has {actualCount} sub-projects");
    }

    public void VerifyFileCount(VssDatabase db, string projectPath, int expectedCount)
    {
        var project = (VssProject)db.GetItem(projectPath);
        var actualCount = project.Files.Count();
        if (actualCount != expectedCount)
            throw new VerificationException(
                $"Project {projectPath}: expected {expectedCount} files, found {actualCount}");

        Console.WriteLine($"  [OK] Project {projectPath} has {actualCount} files");
    }

    public void VerifyLatestAction(VssDatabase db, string projectPath, string fileName, Type expectedActionType)
    {
        var project = (VssProject)db.GetItem(projectPath);
        var file = project.FindFile(fileName);
        if (file == null)
            throw new VerificationException($"File not found: {fileName} in {projectPath}");

        var latestRevision = file.Revisions.Last();
        var actionType = latestRevision.Action.GetType();
        if (actionType != expectedActionType)
            throw new VerificationException(
                $"File {projectPath}/{fileName}: expected latest action {expectedActionType.Name}, found {actionType.Name}");

        Console.WriteLine($"  [OK] File {projectPath}/{fileName} latest action: {actionType.Name}");
    }

    public List<RevisionInfo> GetProjectHistory(VssDatabase db, string projectPath)
    {
        var project = (VssProject)db.GetItem(projectPath);
        var history = new List<RevisionInfo>();

        foreach (var revision in project.Revisions)
        {
            history.Add(new RevisionInfo
            {
                Version = revision.Version,
                DateTime = revision.DateTime,
                User = revision.User,
                ActionType = revision.Action.GetType().Name,
                Comment = revision.Comment,
                Label = revision.Label
            });
        }

        return history;
    }

    public void PrintDatabaseSummary(VssDatabase db)
    {
        Console.WriteLine("  Database Summary:");
        PrintProject(db.RootProject, "    ");
    }

    private void PrintProject(VssProject project, string indent)
    {
        Console.WriteLine($"{indent}{project.Path} ({project.PhysicalName})");
        foreach (var file in project.Files)
            Console.WriteLine($"{indent}  {file.Name} ({file.PhysicalName}) - {file.RevisionCount} revisions");
        foreach (var subProject in project.Projects)
            PrintProject(subProject, indent + "  ");
    }

    private static string Truncate(string s, int maxLength)
    {
        if (s.Length <= maxLength) return s;
        return s.Substring(0, maxLength) + "...";
    }
}

public class RevisionInfo
{
    public int Version { get; set; }
    public DateTime DateTime { get; set; }
    public string User { get; set; } = "";
    public string ActionType { get; set; } = "";
    public string? Comment { get; set; }
    public string? Label { get; set; }
}

public class VerificationException : Exception
{
    public VerificationException(string message) : base(message) { }
}
