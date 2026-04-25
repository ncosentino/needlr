using NexusLabs.Needlr.AgentFramework.Workflows.Sequential;

namespace RfcPipelineApp.Core.Prompts;

/// <summary>
/// Shared utilities for reading workspace files into prompt text.
/// </summary>
internal static class PromptHelpers
{
    /// <summary>
    /// Reads a workspace file and returns its content, or a placeholder if unavailable.
    /// </summary>
    internal static string ReadWorkspaceFile(StageExecutionContext context, string path)
    {
        if (!context.Workspace.FileExists(path))
        {
            return $"[File '{path}' not yet available]";
        }

        var result = context.Workspace.TryReadFile(path);
        if (!result.Success || string.IsNullOrWhiteSpace(result.Value.Content))
        {
            return $"[File '{path}' is empty]";
        }

        return result.Value.Content;
    }
}
