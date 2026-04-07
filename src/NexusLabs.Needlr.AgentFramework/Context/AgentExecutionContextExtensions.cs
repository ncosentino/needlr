namespace NexusLabs.Needlr.AgentFramework.Context;

/// <summary>
/// Extension methods for <see cref="IAgentExecutionContextAccessor"/>.
/// </summary>
public static class AgentExecutionContextExtensions
{
    /// <summary>
    /// Returns the current <see cref="IAgentExecutionContext"/> or throws if none is established.
    /// Tools should call this rather than checking <see cref="IAgentExecutionContextAccessor.Current"/>
    /// directly — there is no valid "anonymous" execution.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no execution context scope is active. This indicates a programming error:
    /// the orchestration layer must call <see cref="IAgentExecutionContextAccessor.BeginScope"/>
    /// before invoking tools.
    /// </exception>
    public static IAgentExecutionContext GetRequired(this IAgentExecutionContextAccessor accessor)
    {
        ArgumentNullException.ThrowIfNull(accessor);

        return accessor.Current ?? throw new InvalidOperationException(
            "Tool invoked outside an agent execution scope. " +
            "The orchestration layer must call IAgentExecutionContextAccessor.BeginScope(context) before invoking tools.");
    }
}
