using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.AgentFramework;
using NexusLabs.Needlr.AgentFramework.Budget;
using NexusLabs.Needlr.AgentFramework.Progress;

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
    public static AgentFrameworkSyringe UsingTokenBudget(
        this AgentFrameworkSyringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);

        return syringe.Configure(opts =>
        {
            var tracker = opts.ServiceProvider.GetRequiredService<ITokenBudgetTracker>();
            var progressAccessor = opts.ServiceProvider.GetRequiredService<IProgressReporterAccessor>();

            var existingFactory = opts.ChatClientFactory;
            opts.ChatClientFactory = sp =>
            {
                var innerClient = existingFactory?.Invoke(sp)
                    ?? sp.GetRequiredService<IChatClient>();

                return new TokenBudgetChatMiddleware(innerClient, tracker, progressAccessor);
            };
        });
    }
}
