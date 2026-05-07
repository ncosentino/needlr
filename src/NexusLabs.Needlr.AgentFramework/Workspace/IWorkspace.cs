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
/// <strong>Path canonicalization contract.</strong> Every method that takes a path
/// argument MUST canonicalize the input via <see cref="WorkspacePath.Canonicalize"/>
/// (file paths) or <see cref="WorkspacePath.CanonicalizeDirectory"/> (directory paths,
/// e.g. the argument to <see cref="ListDirectory"/>) before keying or comparing.
/// Implementations MUST also use <see cref="WorkspacePath.PathComparer"/> (or a comparer
/// with equivalent semantics) for path equality. This guarantees that strings like
/// <c>kb/foo.md</c>, <c>./kb/foo.md</c>, <c>kb//foo.md</c>, <c>kb/./foo.md</c>,
/// <c>/kb/foo.md</c>, and <c>kb/foo.md/</c> all refer to the same logical file. See
/// <see cref="WorkspacePath"/> for the full canonicalization rules and the rejection
/// list (notably: <c>..</c> segments are not permitted).
/// </para>
/// <para>
/// The <c>ActualPath</c> values returned in <see cref="ReadFileResult"/> and
/// <see cref="WriteFileResult"/> are the canonical form. Callers can rely on the
/// returned path being safe to feed back into other workspace methods.
/// </para>
/// <para>
/// <strong>Exceptions vs <see cref="WorkspaceResult{T}"/>.</strong> Invalid path
/// arguments (null, empty, whitespace-only, containing <c>..</c>, canonicalizing to
/// empty for file APIs) throw <see cref="ArgumentNullException"/> or
/// <see cref="ArgumentException"/> directly — they are NOT wrapped in
/// <see cref="WorkspaceResult{T}.Fail"/>. <see cref="WorkspaceResult{T}.Fail"/> is
/// reserved for valid paths where the workspace operation legitimately fails (file
/// missing, compare-exchange content mismatch, etc.).
/// </para>
/// </remarks>
public interface IWorkspace
{
    /// <summary>Reads the entire content of a file.</summary>
    WorkspaceResult<ReadFileResult> TryReadFile(string path);

    /// <summary>Writes content to a file, creating or overwriting it.</summary>
    WorkspaceResult<WriteFileResult> TryWriteFile(string path, string content);

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
    /// <see cref="TryReadFile"/> instead.
    /// </remarks>
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
    /// <paramref name="expectedContent"/>. Returns a structured result indicating
    /// whether the exchange succeeded and why it failed if it didn't.
    /// </summary>
    /// <remarks>
    /// Enables optimistic concurrency for multi-agent pipelines where multiple agents
    /// may edit the same file. Callers read, transform, then CAS — if another agent
    /// wrote in between, the CAS fails and the caller can re-read and retry.
    /// </remarks>
    WorkspaceResult<CompareExchangeResult> TryCompareExchange(string path, string expectedContent, string newContent);
}
