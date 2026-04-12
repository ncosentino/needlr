namespace NexusLabs.Needlr.AgentFramework.Context;

/// <summary>
/// Extension methods for <see cref="IAgentExecutionContextAccessor"/> and
/// <see cref="IAgentExecutionContext"/>.
/// </summary>
public static class AgentExecutionContextExtensions
{
    /// <summary>
    /// Gets the workspace from the context, or <see langword="null"/> if none is set.
    /// Works with both <see cref="AgentExecutionContext"/> (which stores the workspace
    /// in Properties automatically) and custom implementations that put an
    /// <see cref="Workspace.IWorkspace"/> in the property bag under the type key.
    /// </summary>
    public static Workspace.IWorkspace? GetWorkspace(this IAgentExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.GetProperty<Workspace.IWorkspace>();
    }

    /// <summary>
    /// Gets the workspace from the context, throwing if none is set. Convenience for
    /// tool implementations that require a workspace to operate.
    /// </summary>
    /// <exception cref="InvalidOperationException">No workspace is available in the current context.</exception>
    public static Workspace.IWorkspace GetRequiredWorkspace(this IAgentExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.GetWorkspace() ?? throw new InvalidOperationException(
            "No workspace is available in the current execution context. " +
            "The orchestration layer must provide an IWorkspace when constructing the execution context.");
    }

    /// <summary>
    /// Gets a typed property from the context's property bag, keyed by the type's
    /// full name. Returns <see langword="null"/> if not found or if the stored value
    /// is not assignable to <typeparamref name="T"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The default key (<c>typeof(T).FullName</c>) works well when each property type
    /// is unique within a context — the common case for domain records like
    /// <c>ArticleAssignment</c>, <c>SeoReport</c>, etc.
    /// </para>
    /// <para>
    /// <see cref="IAgentExecutionContext.Properties"/> is read-only by design.
    /// Consumers that need to store typed state should populate the property bag at
    /// context construction time via their own <see cref="IAgentExecutionContext"/>
    /// implementation.
    /// </para>
    /// </remarks>
    public static T? GetProperty<T>(this IAgentExecutionContext context) where T : class
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Properties.TryGetValue(typeof(T).FullName!, out var value)
            ? value as T
            : null;
    }

    /// <summary>
    /// Gets a typed property from the context's property bag with an explicit key.
    /// Returns <see langword="null"/> if not found or if the stored value is not
    /// assignable to <typeparamref name="T"/>.
    /// </summary>
    public static T? GetProperty<T>(this IAgentExecutionContext context, string key) where T : class
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Properties.TryGetValue(key, out var value)
            ? value as T
            : null;
    }

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
