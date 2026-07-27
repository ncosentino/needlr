using NexusLabs.Needlr.Injection.Reflection.PluginFactories;
using NexusLabs.Needlr.Injection.Reflection.TypeFilterers;
using NexusLabs.Needlr.Injection.Reflection.TypeRegistrars;
using NexusLabs.Needlr.Injection.TypeFilterers;

using System.Reflection;

using Xunit;

namespace NexusLabs.Needlr.Injection.Tests.Syringes;

/// <summary>
/// Contract coverage for the default-versus-explicitly-supplied component resolution
/// on <see cref="ConfiguredSyringe"/>.
/// </summary>
public sealed class ConfiguredSyringeComponentTests
{
    [Fact]
    public void GetOrCreateTypeRegistrar_NotConfigured_ThrowsInvalidOperationException()
    {
        var syringe = new ConfiguredSyringe();

        var exception = Assert.Throws<InvalidOperationException>(syringe.GetOrCreateTypeRegistrar);

        Assert.Contains("No TypeRegistrar configured", exception.Message);
    }

    [Fact]
    public void GetOrCreateTypeRegistrar_Configured_ReturnsTheSuppliedInstance()
    {
        var typeRegistrar = new RecordingTypeRegistrar();
        var syringe = new ConfiguredSyringe().UsingTypeRegistrar(typeRegistrar);

        Assert.Same(typeRegistrar, syringe.GetOrCreateTypeRegistrar());
    }

    [Fact]
    public void GetOrCreateTypeFilterer_NotConfigured_ReturnsEmptyTypeFilterer()
    {
        var syringe = new ConfiguredSyringe();

        Assert.IsType<EmptyTypeFilterer>(syringe.GetOrCreateTypeFilterer());
    }

    [Fact]
    public void GetOrCreateTypeFilterer_Configured_ReturnsTheSuppliedInstance()
    {
        var typeFilterer = new ReflectionTypeFilterer();
        var syringe = new ConfiguredSyringe().UsingTypeFilterer(typeFilterer);

        Assert.Same(typeFilterer, syringe.GetOrCreateTypeFilterer());
    }

    [Fact]
    public void GetOrCreatePluginFactory_NotConfigured_ThrowsInvalidOperationException()
    {
        var syringe = new ConfiguredSyringe();

        var exception = Assert.Throws<InvalidOperationException>(syringe.GetOrCreatePluginFactory);

        Assert.Contains("No PluginFactory configured", exception.Message);
    }

    [Fact]
    public void GetOrCreatePluginFactory_Configured_ReturnsTheSuppliedInstance()
    {
        var pluginFactory = new ReflectionPluginFactory();
        var syringe = new ConfiguredSyringe().UsingPluginFactory(pluginFactory);

        Assert.Same(pluginFactory, syringe.GetOrCreatePluginFactory());
    }

    [Fact]
    public void GetOrCreateAssemblyProvider_NotConfigured_ThrowsInvalidOperationException()
    {
        var syringe = new ConfiguredSyringe();

        var exception = Assert.Throws<InvalidOperationException>(syringe.GetOrCreateAssemblyProvider);

        Assert.Contains("No AssemblyProvider configured", exception.Message);
    }

    [Fact]
    public void GetOrCreateAssemblyProvider_ConfiguredWithoutOrdering_ReturnsTheSuppliedInstance()
    {
        var assemblyProvider = new RecordingAssemblyProvider([typeof(ConfiguredSyringeComponentTests).Assembly]);
        var syringe = new ConfiguredSyringe().UsingAssemblyProvider(assemblyProvider);

        Assert.Same(assemblyProvider, syringe.GetOrCreateAssemblyProvider());
    }

    [Fact]
    public void GetOrCreateAssemblyProvider_ConfiguredWithOrdering_WrapsAndAppliesTheOrdering()
    {
        var testAssembly = typeof(ConfiguredSyringeComponentTests).Assembly;
        var libraryAssembly = typeof(ConfiguredSyringe).Assembly;
        var assemblyProvider = new RecordingAssemblyProvider([testAssembly, libraryAssembly]);

        var syringe = new ConfiguredSyringe()
            .UsingAssemblyProvider(assemblyProvider)
            .UseTestsLastOrdering();

        var resolved = syringe.GetOrCreateAssemblyProvider();

        Assert.IsType<OrderedAssemblyProvider>(resolved);
        Assert.Equal(
            [libraryAssembly, testAssembly],
            resolved.GetCandidateAssemblies().ToArray());
    }

    [Fact]
    public void GetOrCreateServiceCollectionPopulator_NoFactory_ReturnsDefaultPopulator()
    {
        var syringe = new ConfiguredSyringe();

        var populator = syringe.GetOrCreateServiceCollectionPopulator(
            new ReflectionTypeRegistrar(),
            new ReflectionTypeFilterer(),
            new ReflectionPluginFactory());

        Assert.IsType<ServiceCollectionPopulator>(populator);
    }

    [Fact]
    public void GetOrCreateServiceCollectionPopulator_WithFactory_ReceivesTheSuppliedComponents()
    {
        var typeRegistrar = new RecordingTypeRegistrar();
        var typeFilterer = new ReflectionTypeFilterer();
        var pluginFactory = new ReflectionPluginFactory();

        var syringe = new ConfiguredSyringe().UsingServiceCollectionPopulator(
            (registrar, filterer, factory) => new RecordingServiceCollectionPopulator(registrar, filterer, factory));

        var populator = Assert.IsType<RecordingServiceCollectionPopulator>(
            syringe.GetOrCreateServiceCollectionPopulator(typeRegistrar, typeFilterer, pluginFactory));

        Assert.Same(typeRegistrar, populator.TypeRegistrar);
        Assert.Same(typeFilterer, populator.TypeFilterer);
        Assert.Same(pluginFactory, populator.PluginFactory);
    }

    [Fact]
    public void GetOrCreateServiceProviderBuilder_NoFactory_ThrowsInvalidOperationException()
    {
        var syringe = new ConfiguredSyringe();

        var exception = Assert.Throws<InvalidOperationException>(() => syringe.GetOrCreateServiceProviderBuilder(
            new RecordingServiceCollectionPopulator(
                new RecordingTypeRegistrar(),
                new ReflectionTypeFilterer(),
                new ReflectionPluginFactory()),
            new RecordingAssemblyProvider([]),
            []));

        Assert.Contains("No ServiceProviderBuilderFactory configured", exception.Message);
    }

    [Fact]
    public void GetOrCreateServiceProviderBuilder_WithFactory_ReceivesTheSuppliedComponents()
    {
        var populator = new RecordingServiceCollectionPopulator(
            new RecordingTypeRegistrar(),
            new ReflectionTypeFilterer(),
            new ReflectionPluginFactory());
        var assemblyProvider = new RecordingAssemblyProvider([]);
        IReadOnlyList<Assembly> additionalAssemblies = [typeof(ConfiguredSyringeComponentTests).Assembly];

        var syringe = new ConfiguredSyringe().UsingServiceProviderBuilderFactory(
            (p, a, extra) => new RecordingServiceProviderBuilder(p, a, extra));

        var builder = Assert.IsType<RecordingServiceProviderBuilder>(
            syringe.GetOrCreateServiceProviderBuilder(populator, assemblyProvider, additionalAssemblies));

        Assert.Same(populator, builder.Populator);
        Assert.Same(assemblyProvider, builder.AssemblyProvider);
        Assert.Same(additionalAssemblies, builder.AdditionalAssemblies);
    }

    [Fact]
    public void GetAdditionalAssemblies_NotConfigured_ReturnsEmpty()
    {
        Assert.Empty(new ConfiguredSyringe().GetAdditionalAssemblies());
    }

    [Fact]
    public void GetAdditionalAssemblies_Configured_ReturnsTheSuppliedAssemblies()
    {
        IReadOnlyList<Assembly> assemblies = [typeof(ConfiguredSyringeComponentTests).Assembly];

        var syringe = new ConfiguredSyringe().UsingAdditionalAssemblies(assemblies);

        Assert.Equal(assemblies, syringe.GetAdditionalAssemblies());
    }

    [Fact]
    public void GetPostPluginRegistrationCallbacks_NotConfigured_ReturnsEmpty()
    {
        Assert.Empty(new ConfiguredSyringe().GetPostPluginRegistrationCallbacks());
    }

    [Fact]
    public void GetPostPluginRegistrationCallbacks_Configured_ReturnsCallbacksInRegistrationOrder()
    {
        var invocations = new List<string>();
        var syringe = new ConfiguredSyringe()
            .UsingPostPluginRegistrationCallback(_ => invocations.Add("first"))
            .UsingPostPluginRegistrationCallback(_ => invocations.Add("second"));

        var callbacks = syringe.GetPostPluginRegistrationCallbacks();

        Assert.Equal(2, callbacks.Count);
        foreach (var callback in callbacks)
        {
            callback(new Microsoft.Extensions.DependencyInjection.ServiceCollection());
        }

        Assert.Equal(["first", "second"], invocations);
    }
}
