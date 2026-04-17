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
    public WorkspaceResult<ReadFileResult> TryReadFile(string path)
    {
        var normalized = NormalizePath(path);
        return _files.TryGetValue(normalized, out var content)
            ? WorkspaceResult<ReadFileResult>.Ok(new ReadFileResult(normalized, content))
            : WorkspaceResult<ReadFileResult>.Fail(new FileNotFoundException($"File not found: {normalized}", normalized));
    }

    /// <inheritdoc />
    public WorkspaceResult<WriteFileResult> TryWriteFile(string path, string content)
    {
        var normalized = NormalizePath(path);
        _files[normalized] = content;
        return WorkspaceResult<WriteFileResult>.Ok(new WriteFileResult(normalized, content.Length));
    }

    /// <inheritdoc />
    public bool FileExists(string path) =>
        _files.ContainsKey(NormalizePath(path));

    /// <inheritdoc />
    public IEnumerable<string> GetFilePaths() =>
        _files.Keys;

    /// <inheritdoc />
    public ReadOnlyMemory<char> ReadFileAsMemory(string path)
    {
        var result = TryReadFile(path);
        return result.Success
            ? result.Value.Content.AsMemory()
            : throw result.Exception!;
    }

    /// <inheritdoc />
    public string ListDirectory(string directory, int maxDepth = 2)
    {
        var root = NormalizePath(directory).TrimEnd('/');
        var prefix = root.Length > 0 ? root + "/" : "";

        var entries = _files.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || prefix.Length == 0)
            .Select(k => prefix.Length > 0 ? k[prefix.Length..] : k)
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (entries.Count == 0)
            return root.Length > 0 ? root + "/" : "./";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(root.Length > 0 ? root + "/" : "./");

        var tree = BuildTree(entries);
        RenderTree(sb, tree, indent: "", maxDepth: maxDepth, currentDepth: 0);

        return sb.ToString().TrimEnd();
    }

    private static SortedDictionary<string, object?> BuildTree(List<string> paths)
    {
        var root = new SortedDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            var parts = path.Split('/');
            var current = root;
            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (i == parts.Length - 1)
                {
                    // File leaf
                    current.TryAdd(part, null);
                }
                else
                {
                    // Directory node
                    if (!current.TryGetValue(part, out var child) || child is not SortedDictionary<string, object?> dict)
                    {
                        dict = new SortedDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                        current[part] = dict;
                    }
                    current = dict;
                }
            }
        }
        return root;
    }

    private static void RenderTree(
        System.Text.StringBuilder sb,
        SortedDictionary<string, object?> node,
        string indent,
        int maxDepth,
        int currentDepth)
    {
        if (currentDepth >= maxDepth)
            return;

        var entries = node.ToList();
        for (var i = 0; i < entries.Count; i++)
        {
            var isLast = i == entries.Count - 1;
            var connector = isLast ? "└── " : "├── ";
            var childIndent = indent + (isLast ? "    " : "│   ");

            var (name, child) = entries[i];
            if (child is SortedDictionary<string, object?> dict)
            {
                sb.AppendLine($"{indent}{connector}{name}/");
                RenderTree(sb, dict, childIndent, maxDepth, currentDepth + 1);
            }
            else
            {
                sb.AppendLine($"{indent}{connector}{name}");
            }
        }
    }

    /// <inheritdoc />
    public WorkspaceResult<CompareExchangeResult> TryCompareExchange(string path, string expectedContent, string newContent)
    {
        var normalized = NormalizePath(path);

        if (!_files.TryGetValue(normalized, out var current))
            return WorkspaceResult<CompareExchangeResult>.Fail(
                new FileNotFoundException($"File not found: {normalized}", normalized));

        if (!string.Equals(current, expectedContent, StringComparison.Ordinal))
            return WorkspaceResult<CompareExchangeResult>.Ok(
                new CompareExchangeResult(false, "Content mismatch — file was modified since last read."));

        var exchanged = _files.TryUpdate(normalized, newContent, current);
        return WorkspaceResult<CompareExchangeResult>.Ok(
            new CompareExchangeResult(exchanged, exchanged ? null : "Concurrent modification during exchange."));
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
