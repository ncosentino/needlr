using System;
using System.Collections.Generic;

using NexusLabs.Needlr.Generators.Models;

namespace NexusLabs.Needlr.Generators.Tests.Diagnostics;

/// <summary>
/// Builds <see cref="DiscoveryResult"/> instances for diagnostic artifact tests.
/// </summary>
internal sealed class DiagnosticDiscoveryBuilder
{
    private readonly List<DiscoveredType> _types = new();
    private readonly List<DiscoveredPlugin> _plugins = new();
    private readonly List<DiscoveredDecorator> _decorators = new();
    private readonly List<DiscoveredInterceptedService> _intercepted = new();
    private readonly List<DiscoveredFactory> _factories = new();
    private readonly List<DiscoveredOptions> _options = new();
    private readonly List<DiscoveredHostedService> _hostedServices = new();

    public DiagnosticDiscoveryBuilder WithTypes(params DiscoveredType[] types)
    {
        _types.AddRange(types);
        return this;
    }

    public DiagnosticDiscoveryBuilder WithPlugins(params DiscoveredPlugin[] plugins)
    {
        _plugins.AddRange(plugins);
        return this;
    }

    public DiagnosticDiscoveryBuilder WithDecorators(params DiscoveredDecorator[] decorators)
    {
        _decorators.AddRange(decorators);
        return this;
    }

    public DiagnosticDiscoveryBuilder WithInterceptedServices(params DiscoveredInterceptedService[] intercepted)
    {
        _intercepted.AddRange(intercepted);
        return this;
    }

    public DiagnosticDiscoveryBuilder WithFactories(params DiscoveredFactory[] factories)
    {
        _factories.AddRange(factories);
        return this;
    }

    public DiagnosticDiscoveryBuilder WithOptions(params DiscoveredOptions[] options)
    {
        _options.AddRange(options);
        return this;
    }

    public DiagnosticDiscoveryBuilder WithHostedServices(params DiscoveredHostedService[] hostedServices)
    {
        _hostedServices.AddRange(hostedServices);
        return this;
    }

    public DiscoveryResult Build()
    {
        return new DiscoveryResult(
            _types,
            _plugins,
            _decorators,
            Array.Empty<InaccessibleType>(),
            Array.Empty<MissingTypeRegistryPlugin>(),
            _intercepted,
            _factories,
            _options,
            _hostedServices,
            Array.Empty<DiscoveredProvider>(),
            Array.Empty<DiscoveredHttpClient>(),
            Array.Empty<DiscoveredComposedRegistration>(),
            Array.Empty<ComposedConstraintViolation>());
    }
}
