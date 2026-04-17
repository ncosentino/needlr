using System.ComponentModel;

using NexusLabs.Needlr.AgentFramework;
using NexusLabs.Needlr.AgentFramework.Context;
using NexusLabs.Needlr.AgentFramework.Workspace;

namespace SimpleAgentFrameworkApp.Agents;

/// <summary>
/// Demonstrates the <see cref="IWorkspace"/> pattern: tools read the workspace from the
/// execution context's <see cref="IAgentExecutionContext.Properties"/> bag, then use it
/// to write files. The workspace is opt-in — not forced by the framework.
/// </summary>
[AgentFunctionGroup("geography")]
[AgentFunctionGroup("lifestyle")]
internal sealed class NoteFunctions(IAgentExecutionContextAccessor contextAccessor)
{
    [AgentFunction]
    [Description("Saves a note to the workspace. Use this to record interesting facts about Nick.")]
    public string SaveNote(
        [Description("A short filename for the note (e.g. 'cities.md')")] string filename,
        [Description("The content to save")] string content)
    {
        var ctx = contextAccessor.GetRequired();

        if (!ctx.Properties.TryGetValue("workspace", out var wsObj) || wsObj is not IWorkspace workspace)
            return "No workspace configured — note not saved.";

        workspace.TryWriteFile(filename, content);
        return $"Saved note to {filename} ({content.Length} chars).";
    }
}
