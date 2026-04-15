using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace NexusLabs.Needlr.AgentFramework.Iterative;

/// <summary>
/// Lazily builds and caches the configured <see cref="IChatClient"/> using the same
/// factory chain as <see cref="AgentFactory"/>. This ensures the iterative loop gets
/// the same middleware stack (diagnostics, token budget, etc.) without creating a
/// separate agent.
/// </summary>
[DoNotAutoRegister]
internal sealed class ChatClientAccessor : IChatClientAccessor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IReadOnlyList<Action<AgentFrameworkConfigureOptions>> _configureCallbacks;
    private readonly Lazy<IChatClient> _lazyChatClient;

    internal ChatClientAccessor(
        IServiceProvider serviceProvider,
        IReadOnlyList<Action<AgentFrameworkConfigureOptions>> configureCallbacks)
    {
        _serviceProvider = serviceProvider;
        _configureCallbacks = configureCallbacks;
        _lazyChatClient = new Lazy<IChatClient>(BuildChatClient);
    }

    public IChatClient ChatClient => _lazyChatClient.Value;

    private IChatClient BuildChatClient()
    {
        var options = new AgentFrameworkConfigureOptions
        {
            ServiceProvider = _serviceProvider,
        };

        foreach (var configure in _configureCallbacks)
        {
            configure(options);
        }

        return options.ChatClientFactory?.Invoke(_serviceProvider)
            ?? _serviceProvider.GetRequiredService<IChatClient>();
    }
}
