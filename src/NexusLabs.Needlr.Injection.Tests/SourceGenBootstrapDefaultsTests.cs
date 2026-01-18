using NexusLabs.Needlr.Generators;
using NexusLabs.Needlr.Injection.Loaders;
using NexusLabs.Needlr.Injection.PluginFactories;
using NexusLabs.Needlr.Injection.TypeFilterers;
using NexusLabs.Needlr.Injection.TypeRegistrars;

using Xunit;

namespace NexusLabs.Needlr.Injection.Tests;

public sealed class SourceGenBootstrapDefaultsTests
{
    [Fact]
    public void Syringe_DefaultsToSourceGeneration_WhenBootstrapRegistered()
    {
        var types = new[]
        {
            new InjectableTypeInfo(typeof(FakeService), Array.Empty<Type>(), InjectableLifetime.Singleton)
        };

        var plugins = Array.Empty<PluginTypeInfo>();

        using var _ = NeedlrSourceGenBootstrap.BeginTestScope(() => types, () => plugins);

        var syringe = new Syringe();
        Assert.IsType<GeneratedTypeRegistrar>(syringe.GetOrCreateTypeRegistrar());
        Assert.IsType<GeneratedTypeFilterer>(syringe.GetOrCreateTypeFilterer());
        Assert.IsType<GeneratedPluginFactory>(syringe.GetOrCreatePluginFactory());
        Assert.IsType<GeneratedAssemblyProvider>(syringe.GetOrCreateAssemblyProvider());
    }

    private sealed class FakeService;
}
