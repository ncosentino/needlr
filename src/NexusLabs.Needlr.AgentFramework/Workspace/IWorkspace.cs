namespace NexusLabs.Needlr.AgentFramework.Workspace;

/// <summary>
/// Abstraction for file storage used by agent tools. Implementations may back onto
/// an in-memory dictionary, a real file system, or a cloud blob store.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IWorkspace"/> is intentionally NOT auto-registered in DI. Consumers create
/// workspace instances per-orchestration and attach them wherever they choose (execution
/// context properties, direct injection, etc.). Needlr provides implementations as opt-in
/// building blocks — never forced.
/// </para>
/// <para>
/// All paths use forward-slash separators and are normalized by implementations.
/// </para>
/// </remarks>
public interface IWorkspace
{
    /// <summary>Reads the entire content of a file.</summary>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    string ReadFile(string path);

    /// <summary>Writes content to a file, creating or overwriting it.</summary>
    void WriteFile(string path, string content);

    /// <summary>Returns whether a file exists at the given path.</summary>
    bool FileExists(string path);

    /// <summary>Returns all file paths in the workspace.</summary>
    IEnumerable<string> GetFilePaths();

    /// <summary>
    /// Atomically replaces file content only if the current content matches
    /// <paramref name="expectedContent"/>. Returns <see langword="true"/> if the swap
    /// succeeded, <see langword="false"/> if the content had changed.
    /// </summary>
    /// <remarks>
    /// Enables optimistic concurrency for multi-agent pipelines where multiple agents
    /// may edit the same file. Callers read, transform, then CAS — if another agent
    /// wrote in between, the CAS fails and the caller can re-read and retry.
    /// </remarks>
    bool CompareExchange(string path, string expectedContent, string newContent);
}
