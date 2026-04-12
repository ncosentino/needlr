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
    /// Reads the content of a file as a <see cref="ReadOnlyMemory{T}"/> of <see cref="char"/>.
    /// Implementations that store content as strings can return a zero-copy slice over
    /// the internal string via <see cref="MemoryExtensions"/>, avoiding per-read allocation.
    /// </summary>
    /// <remarks>
    /// Callers that enumerate lines (e.g., <c>MemoryExtensions.EnumerateLines()</c>) benefit
    /// from the zero-copy path. Callers that need a <see cref="string"/> should use
    /// <see cref="ReadFile"/> instead.
    /// </remarks>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    ReadOnlyMemory<char> ReadFileAsMemory(string path);

    /// <summary>
    /// Produces a tree-formatted directory listing starting at <paramref name="directory"/>,
    /// descending up to <paramref name="maxDepth"/> levels. The output format is
    /// implementation-defined but should be human-readable (tree characters, indentation,
    /// file/directory markers).
    /// </summary>
    /// <param name="directory">The root directory to list. Use <c>""</c> or <c>"."</c> for the workspace root.</param>
    /// <param name="maxDepth">Maximum directory depth to descend. Defaults to 2.</param>
    /// <returns>A formatted string representing the directory tree.</returns>
    string ListDirectory(string directory, int maxDepth = 2);

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
