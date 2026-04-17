namespace NexusLabs.Needlr.AgentFramework.Workspace;

/// <summary>Result data for a successful <see cref="IWorkspace.TryWriteFile"/> call.</summary>
/// <param name="ActualPath">The normalized path the file was written to.</param>
/// <param name="CharsWritten">Number of characters written.</param>
public sealed record WriteFileResult(string ActualPath, long CharsWritten);
