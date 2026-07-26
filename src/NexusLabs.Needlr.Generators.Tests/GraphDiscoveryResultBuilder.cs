using NexusLabs.Needlr.Generators.Models;

namespace NexusLabs.Needlr.Generators.Tests;

/// <summary>
/// Builds <see cref="DiscoveryResult"/> instances for graph export tests without
/// requiring a full Roslyn compilation, so every serialization branch of the
/// graph exporter can be exercised with a valid graph scenario.
/// </summary>
internal sealed class GraphDiscoveryResultBuilder
{
    private readonly List<DiscoveredType> _injectableTypes = new();
    private readonly List<DiscoveredPlugin> _pluginTypes = new();
    private readonly List<DiscoveredDecorator> _decorators = new();
    private readonly List<DiscoveredInterceptedService> _interceptedServices = new();
    private readonly List<DiscoveredFactory> _factories = new();
    private readonly List<DiscoveredOptions> _options = new();
    private readonly List<DiscoveredHostedService> _hostedServices = new();

    /// <summary>
    /// Adds an injectable type from the current assembly.
    /// </summary>
    public GraphDiscoveryResultBuilder WithInjectableType(DiscoveredType type)
    {
        _injectableTypes.Add(type);
        return this;
    }

    /// <summary>
    /// Adds a discovered plugin type.
    /// </summary>
    public GraphDiscoveryResultBuilder WithPlugin(DiscoveredPlugin plugin)
    {
        _pluginTypes.Add(plugin);
        return this;
    }

    /// <summary>
    /// Adds a discovered decorator.
    /// </summary>
    public GraphDiscoveryResultBuilder WithDecorator(DiscoveredDecorator decorator)
    {
        _decorators.Add(decorator);
        return this;
    }

    /// <summary>
    /// Adds a discovered intercepted service.
    /// </summary>
    public GraphDiscoveryResultBuilder WithInterceptedService(
        DiscoveredInterceptedService interceptedService)
    {
        _interceptedServices.Add(interceptedService);
        return this;
    }

    /// <summary>
    /// Adds a discovered factory.
    /// </summary>
    public GraphDiscoveryResultBuilder WithFactory(DiscoveredFactory factory)
    {
        _factories.Add(factory);
        return this;
    }

    /// <summary>
    /// Adds a discovered options type.
    /// </summary>
    public GraphDiscoveryResultBuilder WithOptions(DiscoveredOptions options)
    {
        _options.Add(options);
        return this;
    }

    /// <summary>
    /// Adds a discovered hosted service.
    /// </summary>
    public GraphDiscoveryResultBuilder WithHostedService(
        DiscoveredHostedService hostedService)
    {
        _hostedServices.Add(hostedService);
        return this;
    }

    /// <summary>
    /// Creates the discovery result for the configured scenario.
    /// </summary>
    public DiscoveryResult Build()
    {
        return new DiscoveryResult(
            _injectableTypes,
            _pluginTypes,
            _decorators,
            [],
            [],
            _interceptedServices,
            _factories,
            _options,
            _hostedServices,
            [],
            [],
            [],
            []);
    }
}
