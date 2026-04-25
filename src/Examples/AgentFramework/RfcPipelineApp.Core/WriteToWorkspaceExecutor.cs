using NexusLabs.Needlr.AgentFramework.Workflows.Sequential;

namespace RfcPipelineApp.Core;

/// <summary>
/// Decorator that writes the inner executor's response text to a workspace
/// file after successful execution. Supports both overwrite and append modes.
/// </summary>
/// <param name="inner">The executor whose response text will be persisted.</param>
/// <param name="targetPath">Workspace path to write the response to.</param>
/// <param name="overwrite">
/// When <see langword="true"/>, always overwrites the file.
/// When <see langword="false"/> (default), appends to existing non-empty content.
/// </param>
internal sealed class WriteToWorkspaceExecutor(
    IStageExecutor inner,
    string targetPath,
    bool overwrite = false) : IStageExecutor
{
    /// <inheritdoc />
    public async Task<StageExecutionResult> ExecuteAsync(
        StageExecutionContext context,
        CancellationToken cancellationToken)
    {
        var result = await inner.ExecuteAsync(context, cancellationToken);

        if (result.Succeeded && result.ResponseText is { Length: > 0 })
        {
            if (overwrite || !context.Workspace.FileExists(targetPath))
            {
                context.Workspace.TryWriteFile(targetPath, result.ResponseText);
            }
            else
            {
                var existing = context.Workspace.TryReadFile(targetPath);
                if (existing.Success && existing.Value.Content.Length > 0)
                {
                    context.Workspace.TryWriteFile(
                        targetPath,
                        existing.Value.Content + "\n\n" + result.ResponseText);
                }
                else
                {
                    context.Workspace.TryWriteFile(targetPath, result.ResponseText);
                }
            }
        }

        return result;
    }
}
