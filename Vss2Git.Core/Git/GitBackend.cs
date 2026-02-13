namespace Hpdi.Vss2Git
{
    /// <summary>
    /// Specifies which Git backend to use for repository operations.
    /// </summary>
    public enum GitBackend
    {
        /// <summary>
        /// Spawn git.exe processes for each command (default, most compatible).
        /// </summary>
        Process,

        /// <summary>
        /// Use LibGit2Sharp managed library (faster, no git.exe dependency).
        /// </summary>
        LibGit2Sharp
    }
}
