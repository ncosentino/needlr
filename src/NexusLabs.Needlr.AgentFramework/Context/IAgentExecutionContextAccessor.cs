namespace NexusLabs.Needlr.AgentFramework.Context;

/// <summary>
/// Provides access to the ambient <see cref="IAgentExecutionContext"/> for the current async flow.
/// Follows the <c>IHttpContextAccessor</c> pattern: registered as a singleton, backed by
/// <see cref="AsyncLocal{T}"/>, so concurrent orchestrations see their own contexts.
/// </summary>
/// <remarks>
/// <para>
/// Tools MUST NOT take identity fields (UserId, OrchestrationId, etc.) as method parameters.
/// They read them from <see cref="Current"/>. If <see cref="Current"/> is null, the tool should
/// throw — there is no default context, no anonymous user.
/// </para>
/// <para>
/// Trusted callers (API middleware, workflow engine, scenario harness) establish a context by
/// calling <see cref="BeginScope"/> and wrapping agent execution in the returned disposable:
/// </para>
/// <code>
/// using (accessor.BeginScope(context))
/// {
///     await agent.RunAsync(prompt);
/// }
/// </code>
/// </remarks>
public interface IAgentExecutionContextAccessor
{
    /// <summary>
    /// The current execution context for this async flow, or <see langword="null"/> if called
    /// outside any established scope.
    /// </summary>
    IAgentExecutionContext? Current { get; }

    /// <summary>
    /// Establishes a new execution context for the duration of the returned scope. When the
    /// scope is disposed, the previous context (if any) is restored.
    /// </summary>
    IDisposable BeginScope(IAgentExecutionContext context);
}
