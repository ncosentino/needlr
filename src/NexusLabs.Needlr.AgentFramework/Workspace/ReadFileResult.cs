namespace NexusLabs.Needlr.AgentFramework.Workspace;

/// <summary>Result data for a successful <see cref="IWorkspace.TryReadFile"/> call.</summary>
/// <param name="ActualPath">The normalized path the file was read from.</param>
/// <param name="Content">The file content.</param>
public sealed record ReadFileResult(string ActualPath, string Content);
