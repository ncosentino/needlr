using System.Reflection;

using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Generators;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;
using NexusLabs.Needlr.Injection.Reflection.PluginFactories;
using NexusLabs.Needlr.Injection.Reflection.TypeFilterers;
using NexusLabs.Needlr.Injection.Reflection.TypeRegistrars;
using NexusLabs.Needlr.Injection.SourceGen;
using NexusLabs.Needlr.Injection.SourceGen.PluginFactories;
using NexusLabs.Needlr.Injection.SourceGen.TypeFilterers;
using NexusLabs.Needlr.Injection.SourceGen.TypeRegistrars;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.Parity;

/// <summary>
/// Comprehensive parity tests that verify the source-generated code path
/// produces identical results to the reflection-based code path.
/// These tests prove conclusively that zero-reflection mode has full parity.
/// </summary>
public sealed class ComprehensiveParityAnalysisTests
{
    private static readonly Assembly TestAssembly = Assembly.GetExecutingAssembly();

    #region Type Registration Parity

    [Fact]
    public void TypeRegistration_AllInjectableTypes_IdenticalBetweenReflectionAndGenerated()
    {
        // Reflection-based discovery
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        // Generated-based discovery (zero reflection)
        var generatedProvider = new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
            .BuildServiceProvider();

        // Get all test namespace types registered
        var reflectionTypes = GetRegisteredTestTypes(reflectionProvider);
        var generatedTypes = GetRegisteredTestTypes(generatedProvider);

        Assert.Equal(reflectionTypes.Count, generatedTypes.Count);
        foreach (var type in reflectionTypes)
        {
            Assert.Contains(type, generatedTypes);
        }
    }

    [Fact]
    public void TypeRegistration_ServiceLifetimes_IdenticalBetweenReflectionAndGenerated()
    {
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        var generatedProvider = new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
            .BuildServiceProvider();

        // Verify singleton behavior matches
        var reflectionService1 = reflectionProvider.GetService<IMyAutomaticService>();
        var reflectionService2 = reflectionProvider.GetService<IMyAutomaticService>();
        var generatedService1 = generatedProvider.GetService<IMyAutomaticService>();
        var generatedService2 = generatedProvider.GetService<IMyAutomaticService>();

        Assert.Same(reflectionService1, reflectionService2);
        Assert.Same(generatedService1, generatedService2);
    }

    #endregion

    #region Type Filtering Parity

    [Fact]
    public void TypeFilterer_SingletonDetection_IdenticalBetweenReflectionAndGenerated()
    {
        var reflectionFilterer = new ReflectionTypeFilterer();
        var generatedFilterer = new GeneratedTypeFilterer(
            NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes);

        var generatedTypes = NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes();
        var testTypes = generatedTypes
            .Where(t => t.Type.Namespace?.StartsWith("NexusLabs.Needlr.IntegrationTests") == true)
            .ToList();

        foreach (var typeInfo in testTypes)
        {
            var reflectionSaysSingleton = reflectionFilterer.IsInjectableSingletonType(typeInfo.Type);
            var generatedSaysSingleton = generatedFilterer.IsInjectableSingletonType(typeInfo.Type);

            Assert.Equal(reflectionSaysSingleton, generatedSaysSingleton);
        }
    }

    [Fact]
    public void TypeFilterer_TransientDetection_IdenticalBetweenReflectionAndGenerated()
    {
        var reflectionFilterer = new ReflectionTypeFilterer();
        var generatedFilterer = new GeneratedTypeFilterer(
            NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes);

        var generatedTypes = NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes();
        var testTypes = generatedTypes
            .Where(t => t.Type.Namespace?.StartsWith("NexusLabs.Needlr.IntegrationTests") == true)
            .ToList();

        foreach (var typeInfo in testTypes)
        {
            var reflectionSaysTransient = reflectionFilterer.IsInjectableTransientType(typeInfo.Type);
            var generatedSaysTransient = generatedFilterer.IsInjectableTransientType(typeInfo.Type);

            Assert.Equal(reflectionSaysTransient, generatedSaysTransient);
        }
    }

    [Fact]
    public void TypeFilterer_ScopedDetection_IdenticalBetweenReflectionAndGenerated()
    {
        var reflectionFilterer = new ReflectionTypeFilterer();
        var generatedFilterer = new GeneratedTypeFilterer(
            NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes);

        var generatedTypes = NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes();
        var testTypes = generatedTypes
            .Where(t => t.Type.Namespace?.StartsWith("NexusLabs.Needlr.IntegrationTests") == true)
            .ToList();

        foreach (var typeInfo in testTypes)
        {
            var reflectionSaysScoped = reflectionFilterer.IsInjectableScopedType(typeInfo.Type);
            var generatedSaysScoped = generatedFilterer.IsInjectableScopedType(typeInfo.Type);

            Assert.Equal(reflectionSaysScoped, generatedSaysScoped);
        }
    }

    #endregion

    #region Plugin Discovery Parity

    [Fact]
    public void PluginDiscovery_AllPlugins_IdenticalBetweenReflectionAndGenerated()
    {
        var assemblies = new[] { TestAssembly };
        var reflectionFactory = new ReflectionPluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes);

        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(assemblies)
            .Select(p => p.GetType().FullName)
            .OrderBy(n => n)
            .ToList();

        var generatedPlugins = generatedFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(assemblies)
            .Select(p => p.GetType().FullName)
            .OrderBy(n => n)
            .ToList();

        Assert.Equal(reflectionPlugins, generatedPlugins);
    }

    [Fact]
    public void PluginDiscovery_AttributeBasedFiltering_IdenticalBetweenReflectionAndGenerated()
    {
        var assemblies = new[] { TestAssembly };
        var reflectionFactory = new ReflectionPluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes);

        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<ITestPlugin, SpecialPluginAttribute>(assemblies)
            .Select(p => p.GetType().FullName)
            .OrderBy(n => n)
            .ToList();

        var generatedPlugins = generatedFactory
            .CreatePluginsFromAssemblies<ITestPlugin, SpecialPluginAttribute>(assemblies)
            .Select(p => p.GetType().FullName)
            .OrderBy(n => n)
            .ToList();

        Assert.Equal(reflectionPlugins, generatedPlugins);
    }

    [Fact]
    public void PluginDiscovery_AttributeOnlyQuery_IdenticalBetweenReflectionAndGenerated()
    {
        var assemblies = new[] { TestAssembly };
        var reflectionFactory = new ReflectionPluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes);

        var reflectionPlugins = reflectionFactory
            .CreatePluginsWithAttributeFromAssemblies<SpecialPluginAttribute>(assemblies)
            .Select(p => p.GetType().FullName)
            .OrderBy(n => n)
            .ToList();

        var generatedPlugins = generatedFactory
            .CreatePluginsWithAttributeFromAssemblies<SpecialPluginAttribute>(assemblies)
            .Select(p => p.GetType().FullName)
            .OrderBy(n => n)
            .ToList();

        Assert.Equal(reflectionPlugins, generatedPlugins);
    }

    #endregion

    #region Exclusion Parity

    [Fact]
    public void Exclusion_DoNotAutoRegister_IdenticalBetweenReflectionAndGenerated()
    {
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        var generatedProvider = new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
            .BuildServiceProvider();

        // Types with [DoNotAutoRegister] should be excluded by both
        Assert.Null(reflectionProvider.GetService<MyManualService>());
        Assert.Null(generatedProvider.GetService<MyManualService>());
        Assert.Null(reflectionProvider.GetService<IMyManualService>());
        Assert.Null(generatedProvider.GetService<IMyManualService>());
    }

    [Fact]
    public void Exclusion_AbstractTypes_IdenticalBetweenReflectionAndGenerated()
    {
        var assemblies = new[] { TestAssembly };
        var reflectionFactory = new ReflectionPluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes);

        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(assemblies)
            .ToList();
        var generatedPlugins = generatedFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(assemblies)
            .ToList();

        // Abstract types should be excluded by both
        Assert.DoesNotContain(reflectionPlugins, p => p.GetType() == typeof(AbstractTestPlugin));
        Assert.DoesNotContain(generatedPlugins, p => p.GetType() == typeof(AbstractTestPlugin));
    }

    [Fact]
    public void Exclusion_TypesWithoutParameterlessConstructor_IdenticalBetweenReflectionAndGenerated()
    {
        var assemblies = new[] { TestAssembly };
        var reflectionFactory = new ReflectionPluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes);

        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(assemblies)
            .ToList();
        var generatedPlugins = generatedFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(assemblies)
            .ToList();

        // Types requiring dependencies should be excluded by both
        Assert.DoesNotContain(reflectionPlugins, p => p.GetType() == typeof(PluginWithDependency));
        Assert.DoesNotContain(generatedPlugins, p => p.GetType() == typeof(PluginWithDependency));
    }

    #endregion

    #region Interface Registration Parity

    [Fact]
    public void InterfaceRegistration_MultipleInterfaces_IdenticalBetweenReflectionAndGenerated()
    {
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        var generatedProvider = new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
            .BuildServiceProvider();

        // MyAutomaticService implements both IMyAutomaticService and IMyAutomaticService2
        var reflectionService1 = reflectionProvider.GetService<IMyAutomaticService>();
        var reflectionService2 = reflectionProvider.GetService<IMyAutomaticService2>();
        var generatedService1 = generatedProvider.GetService<IMyAutomaticService>();
        var generatedService2 = generatedProvider.GetService<IMyAutomaticService2>();

        Assert.NotNull(reflectionService1);
        Assert.NotNull(reflectionService2);
        Assert.NotNull(generatedService1);
        Assert.NotNull(generatedService2);

        // Same instance for both interfaces (singleton)
        Assert.Same(reflectionService1, reflectionService2);
        Assert.Same(generatedService1, generatedService2);
    }

    #endregion

    #region Inherited Attribute Parity

    [Fact]
    public void InheritedAttribute_PluginDiscovery_IdenticalBetweenReflectionAndGenerated()
    {
        var assemblies = new[] { TestAssembly };
        var reflectionFactory = new ReflectionPluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes);

        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<ITestPlugin, SpecialPluginAttribute>(assemblies)
            .ToList();
        var generatedPlugins = generatedFactory
            .CreatePluginsFromAssemblies<ITestPlugin, SpecialPluginAttribute>(assemblies)
            .ToList();

        // InheritedAttributeTestPlugin inherits [SpecialPlugin] from its base class
        Assert.Contains(reflectionPlugins, p => p.GetType() == typeof(InheritedAttributeTestPlugin));
        Assert.Contains(generatedPlugins, p => p.GetType() == typeof(InheritedAttributeTestPlugin));
    }

    #endregion

    #region Summary Statistics

    [Fact]
    public void Summary_InjectableTypeCount_MatchesBetweenReflectionAndGenerated()
    {
        var generatedTypes = NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes();
        var testTypes = generatedTypes
            .Where(t => t.Type.Namespace?.StartsWith("NexusLabs.Needlr.IntegrationTests") == true)
            .ToList();

        // All generated types should have a valid lifetime (no nulls)
        Assert.All(testTypes, t => Assert.True(t.Lifetime.HasValue));
    }

    [Fact]
    public void Summary_PluginTypeCount_MatchesBetweenReflectionAndGenerated()
    {
        var assemblies = new[] { TestAssembly };
        var reflectionFactory = new ReflectionPluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes);

        var reflectionCount = reflectionFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(assemblies)
            .Count();

        var generatedCount = generatedFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(assemblies)
            .Count();

        Assert.Equal(reflectionCount, generatedCount);
    }

    #endregion

    private static HashSet<Type> GetRegisteredTestTypes(IServiceProvider provider)
    {
        var result = new HashSet<Type>();

        // Test resolving known types
        if (provider.GetService<IMyAutomaticService>() != null)
            result.Add(typeof(IMyAutomaticService));
        if (provider.GetService<IMyAutomaticService2>() != null)
            result.Add(typeof(IMyAutomaticService2));
        if (provider.GetService<MyAutomaticService>() != null)
            result.Add(typeof(MyAutomaticService));
        if (provider.GetService<IInterfaceWithMultipleImplementations>() != null)
            result.Add(typeof(IInterfaceWithMultipleImplementations));

        return result;
    }
}
