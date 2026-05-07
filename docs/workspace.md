# Workspace

`IWorkspace` is Needlr's file-storage abstraction for AI agent tools. It models a logical, rooted key/value store of files — agents read and write paths through this interface without caring whether the backing store is in-memory, on disk, or in cloud blob storage.

## When to use it

Use `IWorkspace` whenever an agent tool needs to read or write files as part of its execution. The dominant pattern is:

1. The pipeline (or agent host) constructs an `IWorkspace` per orchestration.
2. The workspace is attached to `IAgentExecutionContext` (or injected directly).
3. Agent tools resolve it from the context and call its methods.
4. After the agent run completes, the calling code inspects the workspace state.

`IWorkspace` is intentionally **not** auto-registered in DI. Workspaces have per-orchestration lifecycle and must be explicitly constructed.

## The path canonicalization contract

`IWorkspace` is a logical key/value store. The same logical file can be addressed by many spelling variants — `kb/foo.md`, `./kb/foo.md`, `kb//foo.md`, `kb/./foo.md`, `/kb/foo.md`, `kb/foo.md/`, `kb\foo.md`, etc. Without canonicalization, each variant becomes a distinct dictionary key and downstream readers see "missing file" gaps.

Every `IWorkspace` implementation **MUST** canonicalize incoming paths before keying or comparing, and **MUST** use the canonical path comparer for path equality. Needlr ships [`WorkspacePath`](#workspacepath-helpers) as the single source of truth for both.

### Canonical form

The following are all canonicalized to `kb/foo.md`:

| Input | Canonical |
|---|---|
| `kb/foo.md` | `kb/foo.md` |
| `./kb/foo.md` | `kb/foo.md` |
| `kb//foo.md` | `kb/foo.md` |
| `kb/./foo.md` | `kb/foo.md` |
| `/kb/foo.md` | `kb/foo.md` |
| `kb/foo.md/` | `kb/foo.md` |
| `//kb//foo.md//` | `kb/foo.md` |
| `kb\foo.md` | `kb/foo.md` |
| `  kb/foo.md  ` | `kb/foo.md` |

Casing is **preserved** by the canonicalizer (it is structural only) but path equality is **case-insensitive** (`Foo.MD` and `foo.md` are the same key). Implementations must use `WorkspacePath.PathComparer` to enforce this.

### Rejected inputs

| Input | Result |
|---|---|
| `null` | `ArgumentNullException` |
| `""` / `"  "` (file paths) | `ArgumentException` |
| `"/"`, `"//"`, `"."`, `"./"`, `"/./"` (file paths) | `ArgumentException` (canonicalizes to empty) |
| `kb/../foo.md` (any segment exactly `..`) | `ArgumentException` (parent traversal not allowed) |

Filenames that *contain* `..` as a substring (e.g. `version..draft.md`) are accepted — only segments **exactly equal to** `..` are rejected.

For directory-accepting APIs like `IWorkspace.ListDirectory`, the empty string and root-equivalent inputs (`.`, `/`, `./`, `//`, `/./`, whitespace-only) are valid and refer to the workspace root. These are handled by `WorkspacePath.CanonicalizeDirectory`.

## Exceptions vs `WorkspaceResult`

There are two distinct error channels and they mean different things:

| Channel | When | Examples |
|---|---|---|
| `ArgumentNullException` / `ArgumentException` (thrown directly) | The path argument is structurally invalid | `null`, empty, contains `..`, root-equivalent for a file API |
| `WorkspaceResult<T>.Fail(exception)` (returned, not thrown) | The path is valid but the operation cannot complete | File missing, compare-exchange content mismatch |

This separation lets callers distinguish "I passed a bad path" (a programming error worth throwing) from "the file isn't there yet" (an expected control-flow outcome).

## `WorkspacePath` helpers

```csharp
namespace NexusLabs.Needlr.AgentFramework.Workspace;

public static class WorkspacePath
{
    // Case-insensitive ordinal comparer. Implementations MUST use this
    // (or one with equivalent semantics) for path equality.
    public static StringComparer PathComparer { get; }

    // For file paths. Throws on null/empty/whitespace/.. segments/canonicalizes-to-empty.
    public static string Canonicalize(string path);

    // For directory paths (e.g., IWorkspace.ListDirectory). Returns "" for
    // root-equivalent inputs; otherwise same rules as Canonicalize.
    public static string CanonicalizeDirectory(string directory);
}
```

## `InMemoryWorkspace` reference implementation

`InMemoryWorkspace` is the shipping reference implementation, backed by a `ConcurrentDictionary<string, string>` keyed with `WorkspacePath.PathComparer`. It delegates all path inputs to `WorkspacePath.Canonicalize` (for file APIs) or `WorkspacePath.CanonicalizeDirectory` (for `ListDirectory`). It is suitable for tests, sandboxed agent runs, and any scenario where persistence is not needed.

```csharp
var workspace = new InMemoryWorkspace();
workspace.SeedFile("config.json", "{...}");

var content = workspace.TryReadFile("config.json").Value.Content;
```

## Implementing your own `IWorkspace`

When you build a custom `IWorkspace` (e.g., backed by a file system, S3, or a database), follow these rules:

1. Call `WorkspacePath.Canonicalize(path)` at the start of every method that takes a file path.
2. Call `WorkspacePath.CanonicalizeDirectory(directory)` for directory-accepting APIs.
3. Use `WorkspacePath.PathComparer` (or an equivalent) for any dictionary, set, or string comparison that compares paths.
4. Return canonical paths in `ReadFileResult.ActualPath` and `WriteFileResult.ActualPath` — callers can then feed those values back into other workspace methods.
5. Let `ArgumentException` and `ArgumentNullException` propagate. Don't wrap them in `WorkspaceResult.Fail`.

## The decorator pattern

A common extension is wrapping an inner `IWorkspace` to add cross-cutting behavior — write-path allowlists, audit logging, copy-on-write to a real file system, etc. Because canonicalization happens inside the inner workspace, decorators can either:

- **Pass paths through unchanged** and let the inner workspace canonicalize (simplest, recommended).
- **Canonicalize at the decorator boundary** if the decorator needs to make decisions based on the canonical form (e.g., "is this path under `/allowed/`?").

```csharp
public sealed class AllowedWritePathsWorkspace : IWorkspace
{
    private readonly IWorkspace _inner;
    private readonly HashSet<string> _allowedPrefixes;

    public AllowedWritePathsWorkspace(IWorkspace inner, IEnumerable<string> allowedPrefixes)
    {
        _inner = inner;
        _allowedPrefixes = new HashSet<string>(
            allowedPrefixes.Select(WorkspacePath.Canonicalize),
            WorkspacePath.PathComparer);
    }

    public WorkspaceResult<WriteFileResult> TryWriteFile(string path, string content)
    {
        var canonical = WorkspacePath.Canonicalize(path);
        if (!_allowedPrefixes.Any(p => canonical.StartsWith(p + "/", StringComparison.OrdinalIgnoreCase)))
        {
            return WorkspaceResult<WriteFileResult>.Fail(
                new UnauthorizedAccessException($"Writes to '{canonical}' are not allowed."));
        }
        return _inner.TryWriteFile(canonical, content);
    }

    // ...delegate other methods to _inner
}
```

## Migration notes

If you previously relied on `InMemoryWorkspace`'s un-canonicalized aliasing behavior, the following changes are now in effect:

- Paths like `kb/foo.md`, `./kb/foo.md`, `kb//foo.md`, `kb/./foo.md`, `/kb/foo.md`, and `kb/foo.md/` now collide as the same logical file. If you were relying on these to produce distinct keys (unusual), switch to using genuinely different paths.
- Paths containing `..` segments (e.g., `kb/../foo.md`) now throw `ArgumentException`. Replace with the resolved workspace-relative path (e.g., `foo.md`).
- File-mutating methods reject empty / root-equivalent paths (`""`, `"/"`, `"."`, `"./"`). `ListDirectory` continues to accept these as the workspace root.

## See also

- [Iterative Agent Loop](iterative-agent-loop.md) — the most common consumer of `IWorkspace` (workspaces are attached to `IterativeContext`).
- [Testing Tools](testing-tools.md) — `ToolInvocationRunner.WithWorkspace(...)` lets you seed a workspace for tool tests.
