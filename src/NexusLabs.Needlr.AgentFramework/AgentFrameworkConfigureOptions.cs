using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.AgentFramework;

/// <summary>
/// Factory-level configuration options passed to
/// <see cref="AgentFrameworkSyringeExtensions.Configure"/> callbacks.
/// </summary>
public sealed class AgentFrameworkConfigureOptions
{
    /// <summary>
    /// Gets the service provider from the DI container.
    /// Use this to resolve configuration or services needed to create the <see cref="IChatClient"/>.
    /// </summary>
    public required IServiceProvider ServiceProvider { get; init; }

    /// <summary>
    /// Gets or sets a factory that creates the <see cref="IChatClient"/> used by all agents
    /// built from this factory. When <see langword="null"/>,
    /// <see cref="IChatClient"/> is resolved from the DI container.
    /// </summary>
    public Func<IServiceProvider, IChatClient>? ChatClientFactory { get; set; }

    /// <summary>
    /// Gets or sets the default system instructions applied to all agents
    /// unless overridden via <see cref="AgentFactoryOptions.Instructions"/>.
    /// </summary>
    public string? DefaultInstructions { get; set; }
}
