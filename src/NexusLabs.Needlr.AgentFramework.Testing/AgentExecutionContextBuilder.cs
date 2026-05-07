using NexusLabs.Needlr.AgentFramework.Context;
using NexusLabs.Needlr.AgentFramework.Workspace;

namespace NexusLabs.Needlr.AgentFramework.Testing;

/// <summary>
/// Mutable builder used by
/// <see cref="ToolInvocationRunner.WithExecutionContext(Action{AgentExecutionContextBuilder})"/>
/// to configure the <see cref="IAgentExecutionContext"/> a tool sees during invocation.
/// </summary>
/// <remarks>
/// <para>
/// A fresh <see cref="AgentExecutionContextBuilder"/> instance is created per
/// <see cref="ToolInvocationRunner.WithExecutionContext(Action{AgentExecutionContextBuilder})"/>
/// call so that successive runner instances do not share mutable state.
/// </para>
/// <para>
/// The resulting context is established via
/// <see cref="IAgentExecutionContextAccessor.BeginScope(IAgentExecutionContext)"/> for the
/// duration of a single <c>InvokeAsync</c> call, then disposed.
/// </para>
/// </remarks>
public sealed class AgentExecutionContextBuilder
{
    private string _userId = "tool-invocation-runner";
    private string _orchestrationId = $"tool-invocation-{Guid.NewGuid():N}";
    private IWorkspace? _workspace;
    private Dictionary<string, object>? _properties;

    /// <summary>
    /// Sets the <see cref="IAgentExecutionContext.UserId"/> for the invocation context.
    /// Default is <c>"tool-invocation-runner"</c>.
    /// </summary>
    public AgentExecutionContextBuilder WithUserId(string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        _userId = userId;
        return this;
    }

    /// <summary>
    /// Sets the <see cref="IAgentExecutionContext.OrchestrationId"/> for the invocation context.
    /// Default is a fresh GUID-derived string per builder.
    /// </summary>
    public AgentExecutionContextBuilder WithOrchestrationId(string orchestrationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orchestrationId);
        _orchestrationId = orchestrationId;
        return this;
    }

    /// <summary>
    /// Creates a fresh <see cref="InMemoryWorkspace"/>, runs <paramref name="seed"/> against it,
    /// and attaches it to the execution context. This is the most common path: a per-invocation
    /// throw-away workspace pre-populated with fixture files.
    /// </summary>
    /// <param name="seed">Callback that writes seed files to the workspace.</param>
    /// <remarks>
    /// Calling this multiple times replaces the previously-attached workspace; only the last call
    /// wins. If a workspace was already supplied via the
    /// <see cref="WithWorkspace(IWorkspace)"/> overload, it is replaced.
    /// </remarks>
    public AgentExecutionContextBuilder WithWorkspace(Action<IWorkspace> seed)
    {
        ArgumentNullException.ThrowIfNull(seed);
        var workspace = new InMemoryWorkspace();
        seed(workspace);
        _workspace = workspace;
        return this;
    }

    /// <summary>
    /// Attaches a caller-supplied <see cref="IWorkspace"/> to the execution context.
    /// Use this when the test needs a workspace implementation other than
    /// <see cref="InMemoryWorkspace"/>, or wants to share a workspace across invocations.
    /// </summary>
    public AgentExecutionContextBuilder WithWorkspace(IWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        _workspace = workspace;
        return this;
    }

    /// <summary>
    /// Adds a custom property to the execution context's
    /// <see cref="IAgentExecutionContext.Properties"/> bag. Useful for tools that read tenant
    /// IDs, correlation tokens, or other ambient data via
    /// <see cref="AgentExecutionContextExtensions.GetProperty{T}(IAgentExecutionContext, string)"/>.
    /// </summary>
    public AgentExecutionContextBuilder WithProperty(string key, object value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        _properties ??= [];
        _properties[key] = value;
        return this;
    }

    internal IWorkspace? Workspace => _workspace;

    internal IAgentExecutionContext Build() =>
        new AgentExecutionContext(
            UserId: _userId,
            OrchestrationId: _orchestrationId,
            Properties: _properties,
            Workspace: _workspace);
}
