using System.Collections.Concurrent;

namespace NexusLabs.Needlr.AgentFramework.Workspace;

/// <summary>
/// Thread-safe in-memory <see cref="IWorkspace"/> backed by a <see cref="ConcurrentDictionary{TKey, TValue}"/>.
/// Suitable for testing, sandboxed agent runs, and scenarios where persistence is not needed.
/// </summary>
/// <remarks>
/// <para>
/// Marked <see cref="DoNotAutoRegisterAttribute"/> to prevent Needlr from registering it as a
/// singleton — workspaces have per-orchestration lifecycle and must be explicitly constructed.
/// </para>
/// <para>
/// Paths are normalized: backslashes are replaced with forward slashes, leading slashes are trimmed,
/// and comparison is case-insensitive (matching Windows file system behavior by default).
/// </para>
/// </remarks>
[DoNotAutoRegister]
public sealed class InMemoryWorkspace : IWorkspace
{
    private readonly ConcurrentDictionary<string, string> _files =
        new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public string ReadFile(string path)
    {
        var normalized = NormalizePath(path);
        return _files.TryGetValue(normalized, out var content)
            ? content
            : throw new FileNotFoundException($"File not found: {normalized}", normalized);
    }

    /// <inheritdoc />
    public void WriteFile(string path, string content)
    {
        var normalized = NormalizePath(path);
        _files[normalized] = content;
    }

    /// <inheritdoc />
    public bool FileExists(string path) =>
        _files.ContainsKey(NormalizePath(path));

    /// <inheritdoc />
    public IEnumerable<string> GetFilePaths() =>
        _files.Keys;

    /// <inheritdoc />
    public bool CompareExchange(string path, string expectedContent, string newContent)
    {
        var normalized = NormalizePath(path);

        if (!_files.TryGetValue(normalized, out var current))
            return false;

        if (!string.Equals(current, expectedContent, StringComparison.Ordinal))
            return false;

        // ConcurrentDictionary.TryUpdate uses reference equality for the comparand,
        // so we pass the exact reference we just read.
        return _files.TryUpdate(normalized, newContent, current);
    }

    /// <summary>
    /// Seeds the workspace with a file before agent execution begins.
    /// Convenience method for test setup and scenario harnesses.
    /// </summary>
    public void SeedFile(string path, string content) =>
        _files[NormalizePath(path)] = content;

    private static string NormalizePath(string path) =>
        path.Replace('\\', '/').TrimStart('/');
}
