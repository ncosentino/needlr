using NexusLabs.Needlr.Generators;
using NexusLabs.Needlr.Injection.SourceGen;
using NexusLabs.Needlr.Injection.SourceGen.Loaders;
using NexusLabs.Needlr.Injection.SourceGen.PluginFactories;
using NexusLabs.Needlr.Injection.SourceGen.TypeFilterers;
using NexusLabs.Needlr.Injection.SourceGen.TypeRegistrars;

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

        // With new architecture, we must explicitly call UsingSourceGen() to use source-generated components
        var syringe = new Syringe().UsingSourceGen();
        Assert.IsType<GeneratedTypeRegistrar>(syringe.GetOrCreateTypeRegistrar());
        Assert.IsType<GeneratedTypeFilterer>(syringe.GetOrCreateTypeFilterer());
        Assert.IsType<GeneratedPluginFactory>(syringe.GetOrCreatePluginFactory());
        Assert.IsType<GeneratedAssemblyProvider>(syringe.GetOrCreateAssemblyProvider());
    }

    private sealed class FakeService;
}
