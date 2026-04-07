using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.AgentFramework;
using NexusLabs.Needlr.AgentFramework.Budget;

namespace NexusLabs.Needlr.AgentFramework.Workflows.Budget;

/// <summary>
/// Extension methods for wiring token-budget enforcement into the agent framework.
/// </summary>
public static class TokenBudgetExtensions
{
    /// <summary>
    /// Wraps the configured <see cref="IChatClient"/> with <see cref="TokenBudgetChatMiddleware"/>,
    /// enabling per-pipeline token budgets via <see cref="ITokenBudgetTracker"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="ITokenBudgetTracker"/> is automatically registered by <c>UsingAgentFramework()</c>
    /// — no separate DI registration is required.
    /// <code>
    /// var sp = new Syringe()
    ///     .UsingReflection()
    ///     .UsingAgentFramework(af => af
    ///         .UsingChatClient(rawChatClient)
    ///         .UsingTokenBudget())
    ///     .BuildServiceProvider(config);
    ///
    /// // In a pipeline orchestrator (ITokenBudgetTracker injected via DI):
    /// using var _ = _tracker.BeginScope(maxTokens: 5000);
    /// await workflow.RunAsync(prompt, ct);
    /// </code>
    /// </remarks>
    public static AgentFrameworkSyringe UsingTokenBudget(
        this AgentFrameworkSyringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);

        return syringe.Configure(opts =>
        {
            var tracker = opts.ServiceProvider.GetRequiredService<ITokenBudgetTracker>();

            var existingFactory = opts.ChatClientFactory;
            opts.ChatClientFactory = sp =>
            {
                var innerClient = existingFactory?.Invoke(sp)
                    ?? sp.GetRequiredService<IChatClient>();

                return new TokenBudgetChatMiddleware(innerClient, tracker);
            };
        });
    }
}
