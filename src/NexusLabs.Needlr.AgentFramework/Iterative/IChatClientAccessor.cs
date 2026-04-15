using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.AgentFramework.Iterative;

/// <summary>
/// Provides access to the configured <see cref="IChatClient"/> from the agent framework
/// pipeline, including any middleware (diagnostics, token budget) that was wired via
/// <c>UsingDiagnostics()</c>, <c>UsingTokenBudget()</c>, etc.
/// </summary>
/// <remarks>
/// <para>
/// This is the same <see cref="IChatClient"/> instance that <see cref="IAgentFactory"/>
/// uses internally when creating agents. Exposing it separately allows components like
/// <see cref="IIterativeAgentLoop"/> to make raw LLM calls without going through the
/// <c>FunctionInvokingChatClient</c> that <c>AIAgent</c> wraps around the client.
/// </para>
/// <para>
/// The client is lazily created on first access and cached for the lifetime of the
/// service provider.
/// </para>
/// </remarks>
public interface IChatClientAccessor
{
    /// <summary>
    /// Gets the configured <see cref="IChatClient"/> with all middleware applied.
    /// </summary>
    IChatClient ChatClient { get; }
}
