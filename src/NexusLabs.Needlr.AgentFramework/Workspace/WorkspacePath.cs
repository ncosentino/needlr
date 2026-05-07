namespace NexusLabs.Needlr.AgentFramework.Workspace;

/// <summary>
/// Canonicalization helpers for <see cref="IWorkspace"/> paths.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IWorkspace"/> is a logical, rooted key/value store: a path identifies a
/// single file regardless of how the caller spells it. Without canonicalization, the
/// strings <c>kb/foo.md</c>, <c>./kb/foo.md</c>, <c>kb//foo.md</c>, <c>kb/./foo.md</c>,
/// <c>/kb/foo.md</c>, and <c>kb/foo.md/</c> would all be distinct keys despite
/// referring to the same logical file. This class defines the canonical form every
/// implementation MUST produce before keying or comparing paths.
/// </para>
/// <para>
/// The canonical form is structural only — it does NOT lower-case or re-encode the
/// string. Path equality is performed by <see cref="PathComparer"/>, which is
/// case-insensitive (<see cref="StringComparer.OrdinalIgnoreCase"/>). Implementations
/// MUST use this comparer (or one with equivalent semantics) when storing or comparing
/// canonicalized paths.
/// </para>
/// <para>
/// Use <see cref="Canonicalize"/> for paths that identify a file (the common case). Use
/// <see cref="CanonicalizeDirectory"/> for paths that identify a directory (e.g., the
/// <c>directory</c> argument to <see cref="IWorkspace.ListDirectory"/>) — the directory
/// variant accepts root-equivalent inputs (<c>""</c>, <c>"."</c>, <c>"/"</c>, etc.) and
/// returns the workspace-root sentinel (<c>""</c>), whereas the file variant rejects
/// them.
/// </para>
/// <para>
/// Invalid paths throw <see cref="ArgumentNullException"/> or
/// <see cref="ArgumentException"/> directly. They are NOT wrapped in
/// <see cref="WorkspaceResult{T}.Fail"/>. <see cref="WorkspaceResult{T}.Fail"/> is
/// reserved for valid paths where the workspace operation legitimately fails (file
/// missing, compare-exchange mismatch, etc.).
/// </para>
/// </remarks>
public static class WorkspacePath
{
    /// <summary>
    /// The <see cref="StringComparer"/> implementations MUST use for path equality.
    /// Case-insensitive, ordinal — matches the dominant file-system convention on
    /// Windows and macOS, and aligns with the historical
    /// <see cref="InMemoryWorkspace"/> behavior.
    /// </summary>
    public static StringComparer PathComparer { get; } = StringComparer.OrdinalIgnoreCase;

    /// <summary>
    /// Canonicalizes a workspace file path.
    /// </summary>
    /// <remarks>
    /// <para>Rules applied, in order:</para>
    /// <list type="number">
    ///   <item><description>Outer whitespace is trimmed (segment-internal whitespace is preserved).</description></item>
    ///   <item><description>Backslashes (<c>\</c>) are replaced with forward slashes (<c>/</c>).</description></item>
    ///   <item><description><c>.</c> segments are dropped (so <c>./kb/foo</c> becomes <c>kb/foo</c>).</description></item>
    ///   <item><description>Empty segments are collapsed (so <c>kb//foo</c> becomes <c>kb/foo</c>).</description></item>
    ///   <item><description>Leading and trailing slashes are stripped (so <c>/kb/foo/</c> becomes <c>kb/foo</c>).</description></item>
    /// </list>
    /// <para>The following inputs are rejected:</para>
    /// <list type="bullet">
    ///   <item><description><see langword="null"/> → <see cref="ArgumentNullException"/>.</description></item>
    ///   <item><description>Empty or whitespace-only string → <see cref="ArgumentException"/>.</description></item>
    ///   <item><description>Any segment exactly equal to <c>..</c> (parent traversal) → <see cref="ArgumentException"/>. Filenames that merely <em>contain</em> <c>..</c> as a substring (e.g., <c>version..draft.md</c>) are accepted.</description></item>
    ///   <item><description>Inputs that canonicalize to the empty string (<c>"/"</c>, <c>"//"</c>, <c>"."</c>, <c>"./"</c>, <c>"/./"</c>, …) → <see cref="ArgumentException"/>. Use <see cref="CanonicalizeDirectory"/> if you need to express the workspace root.</description></item>
    /// </list>
    /// </remarks>
    /// <param name="path">The path to canonicalize.</param>
    /// <returns>The canonical form of <paramref name="path"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty, whitespace-only, contains a <c>..</c> segment, or canonicalizes to the empty string.</exception>
    /// <example>
    /// <code>
    /// WorkspacePath.Canonicalize("kb/foo.md")        // → "kb/foo.md"
    /// WorkspacePath.Canonicalize("./kb/foo.md")      // → "kb/foo.md"
    /// WorkspacePath.Canonicalize("kb//foo.md")       // → "kb/foo.md"
    /// WorkspacePath.Canonicalize(@"kb\foo.md")       // → "kb/foo.md"
    /// WorkspacePath.Canonicalize("/kb/foo.md/")      // → "kb/foo.md"
    /// WorkspacePath.Canonicalize("kb/../foo.md")     // throws ArgumentException
    /// WorkspacePath.Canonicalize("/")                // throws ArgumentException
    /// </code>
    /// </example>
    public static string Canonicalize(string path)
    {
        var canonical = CanonicalizeCore(path, allowRoot: false);
        if (canonical.Length == 0)
        {
            throw new ArgumentException(
                $"Workspace file path cannot be empty after canonicalization (input: '{path}'). Use WorkspacePath.CanonicalizeDirectory if you intended the workspace root.",
                nameof(path));
        }

        return canonical;
    }

    /// <summary>
    /// Canonicalizes a workspace directory path. Use this for the <c>directory</c>
    /// argument to <see cref="IWorkspace.ListDirectory"/> and any other API that
    /// accepts a directory.
    /// </summary>
    /// <remarks>
    /// <para>Same rules as <see cref="Canonicalize"/>, with one difference:
    /// root-equivalent inputs (<c>""</c>, whitespace-only, <c>"."</c>, <c>"./"</c>,
    /// <c>"/"</c>, <c>"//"</c>, <c>"/./"</c>, …) all return the empty string,
    /// representing the workspace root. <see cref="Canonicalize"/> rejects these.
    /// </para>
    /// <para>
    /// <see langword="null"/> still throws <see cref="ArgumentNullException"/>, and
    /// segment-exact <c>..</c> still throws <see cref="ArgumentException"/>.
    /// </para>
    /// </remarks>
    /// <param name="directory">The directory path to canonicalize.</param>
    /// <returns>
    /// The canonical form of <paramref name="directory"/>, or the empty string for
    /// root-equivalent inputs.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="directory"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="directory"/> contains a <c>..</c> segment.</exception>
    /// <example>
    /// <code>
    /// WorkspacePath.CanonicalizeDirectory("")        // → ""
    /// WorkspacePath.CanonicalizeDirectory(".")       // → ""
    /// WorkspacePath.CanonicalizeDirectory("/")       // → ""
    /// WorkspacePath.CanonicalizeDirectory("./src")   // → "src"
    /// WorkspacePath.CanonicalizeDirectory("src/")    // → "src"
    /// WorkspacePath.CanonicalizeDirectory("src//")   // → "src"
    /// WorkspacePath.CanonicalizeDirectory("../src")  // throws ArgumentException
    /// </code>
    /// </example>
    public static string CanonicalizeDirectory(string directory) =>
        CanonicalizeCore(directory, allowRoot: true);

    private static string CanonicalizeCore(string path, bool allowRoot)
    {
        if (path is null)
            throw new ArgumentNullException(nameof(path));

        var trimmed = path.Trim();
        if (trimmed.Length == 0)
        {
            if (allowRoot)
                return string.Empty;
            throw new ArgumentException(
                "Workspace file path cannot be empty or whitespace.",
                nameof(path));
        }

        var slashed = trimmed.Replace('\\', '/');

        var segments = slashed.Split('/');
        var canonical = new System.Text.StringBuilder(slashed.Length);
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];

            if (segment.Length == 0 || segment == ".")
                continue;

            if (segment == "..")
            {
                throw new ArgumentException(
                    $"Workspace path may not contain '..' segments (input: '{path}'). Workspace paths are rooted at the workspace; pass the resolved workspace-relative path directly.",
                    nameof(path));
            }

            if (canonical.Length > 0)
                canonical.Append('/');
            canonical.Append(segment);
        }

        return canonical.ToString();
    }
}
