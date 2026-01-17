using System.Reflection;

using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Extensions.Configuration;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.PluginFactories;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests;

/// <summary>
/// Integration tests that verify exact parity between the reflection-based
/// type discovery path (DefaultTypeRegistrar) and the source generation path
/// (GeneratedTypeRegistrar).
/// </summary>
/// <remarks>
/// These tests ensure that swapping out the type registrar between reflection
/// and source generation produces identical results. This is critical for
/// ensuring that source generation is a drop-in replacement for reflection.
/// </remarks>
public sealed class ReflectionVsSourceGenerationParityTests
{
    private readonly IServiceProvider _reflectionProvider;
    private readonly IServiceProvider _generatedProvider;

    public ReflectionVsSourceGenerationParityTests()
    {
        // Build service provider using reflection (DefaultTypeRegistrar)
        _reflectionProvider = new Syringe()
            .UsingDefaultTypeRegistrar()
            .BuildServiceProvider();

        // Build service provider using source generation (GeneratedTypeRegistrar)
        // Uses the generated TypeRegistry from [assembly: GenerateTypeRegistry]
        _generatedProvider = new Syringe()
            .UsingGeneratedTypeRegistrar(
                NexusLabs.Needlr.Generated.TypeRegistry.GetInjectableTypes)
            .BuildServiceProvider();
    }

    [Fact]
    public void Parity_IMyAutomaticService_BothProvidersResolve()
    {
        var reflectionService = _reflectionProvider.GetService<IMyAutomaticService>();
        var generatedService = _generatedProvider.GetService<IMyAutomaticService>();

        Assert.NotNull(reflectionService);
        Assert.NotNull(generatedService);
        Assert.Equal(reflectionService.GetType(), generatedService.GetType());
    }

    [Fact]
    public void Parity_IMyAutomaticService2_BothProvidersResolve()
    {
        var reflectionService = _reflectionProvider.GetService<IMyAutomaticService2>();
        var generatedService = _generatedProvider.GetService<IMyAutomaticService2>();

        Assert.NotNull(reflectionService);
        Assert.NotNull(generatedService);
        Assert.Equal(reflectionService.GetType(), generatedService.GetType());
    }

    [Fact]
    public void Parity_MyAutomaticService_BothProvidersResolve()
    {
        var reflectionService = _reflectionProvider.GetService<MyAutomaticService>();
        var generatedService = _generatedProvider.GetService<MyAutomaticService>();

        Assert.NotNull(reflectionService);
        Assert.NotNull(generatedService);
    }

    [Fact]
    public void Parity_SingletonBehavior_IMyAutomaticService_SameInstance()
    {
        var r1 = _reflectionProvider.GetService<IMyAutomaticService>();
        var r2 = _reflectionProvider.GetService<IMyAutomaticService>();
        Assert.Same(r1, r2);

        var g1 = _generatedProvider.GetService<IMyAutomaticService>();
        var g2 = _generatedProvider.GetService<IMyAutomaticService>();
        Assert.Same(g1, g2);
    }

    [Fact]
    public void Parity_SingletonBehavior_InterfacesShareInstance()
    {
        var r1 = _reflectionProvider.GetService<IMyAutomaticService>();
        var r2 = _reflectionProvider.GetService<IMyAutomaticService2>();
        Assert.Same(r1, r2);

        var g1 = _generatedProvider.GetService<IMyAutomaticService>();
        var g2 = _generatedProvider.GetService<IMyAutomaticService2>();
        Assert.Same(g1, g2);
    }

    [Fact]
    public void Parity_SingletonBehavior_ConcreteAndInterfaceShareInstance()
    {
        var r1 = _reflectionProvider.GetService<MyAutomaticService>();
        var r2 = _reflectionProvider.GetService<IMyAutomaticService>();
        Assert.Same(r1, r2);

        var g1 = _generatedProvider.GetService<MyAutomaticService>();
        var g2 = _generatedProvider.GetService<IMyAutomaticService>();
        Assert.Same(g1, g2);
    }

    [Fact]
    public void Parity_IInterfaceWithMultipleImplementations_BothProvidersResolveEnumerable()
    {
        var reflectionInstances = _reflectionProvider
            .GetService<IEnumerable<IInterfaceWithMultipleImplementations>>()
            ?.ToList();
        var generatedInstances = _generatedProvider
            .GetService<IEnumerable<IInterfaceWithMultipleImplementations>>()
            ?.ToList();

        Assert.NotNull(reflectionInstances);
        Assert.NotNull(generatedInstances);
        Assert.NotEmpty(reflectionInstances);
        Assert.NotEmpty(generatedInstances);

        // Same number of implementations
        Assert.Equal(reflectionInstances.Count, generatedInstances.Count);

        // Same implementation types
        var reflectionTypes = reflectionInstances.Select(x => x.GetType()).OrderBy(t => t.Name).ToList();
        var generatedTypes = generatedInstances.Select(x => x.GetType()).OrderBy(t => t.Name).ToList();
        Assert.Equal(reflectionTypes, generatedTypes);
    }

    [Fact]
    public void Parity_ImplementationA_BothProvidersResolve()
    {
        var reflectionService = _reflectionProvider.GetService<ImplementationA>();
        var generatedService = _generatedProvider.GetService<ImplementationA>();

        Assert.NotNull(reflectionService);
        Assert.NotNull(generatedService);
    }

    [Fact]
    public void Parity_ImplementationB_BothProvidersResolve()
    {
        var reflectionService = _reflectionProvider.GetService<ImplementationB>();
        var generatedService = _generatedProvider.GetService<ImplementationB>();

        Assert.NotNull(reflectionService);
        Assert.NotNull(generatedService);
    }

    [Fact]
    public void Parity_MyManualService_BothProvidersExclude()
    {
        // Types marked with [DoNotAutoRegister] should NOT be registered by either path
        var reflectionService = _reflectionProvider.GetService<MyManualService>();
        var generatedService = _generatedProvider.GetService<MyManualService>();

        Assert.Null(reflectionService);
        Assert.Null(generatedService);
    }

    [Fact]
    public void Parity_IMyManualService_BothProvidersExclude()
    {
        // Interfaces implemented by [DoNotAutoRegister] types should NOT be registered
        var reflectionService = _reflectionProvider.GetService<IMyManualService>();
        var generatedService = _generatedProvider.GetService<IMyManualService>();

        Assert.Null(reflectionService);
        Assert.Null(generatedService);
    }

    [Fact]
    public void Parity_TestServiceToBeDecorated_BothProvidersExclude()
    {
        var reflectionService = _reflectionProvider.GetService<TestServiceToBeDecorated>();
        var generatedService = _generatedProvider.GetService<TestServiceToBeDecorated>();

        Assert.Null(reflectionService);
        Assert.Null(generatedService);
    }

    [Fact]
    public void Parity_LazyIMyAutomaticService_BothProvidersResolve()
    {
        var reflectionLazy = _reflectionProvider.GetService<Lazy<IMyAutomaticService>>();
        var generatedLazy = _generatedProvider.GetService<Lazy<IMyAutomaticService>>();

        Assert.NotNull(reflectionLazy);
        Assert.NotNull(generatedLazy);
        Assert.NotNull(reflectionLazy.Value);
        Assert.NotNull(generatedLazy.Value);
        Assert.Equal(reflectionLazy.Value.GetType(), generatedLazy.Value.GetType());
    }

    [Fact]
    public void Parity_LazyMyAutomaticService_BothProvidersResolve()
    {
        var reflectionLazy = _reflectionProvider.GetService<Lazy<MyAutomaticService>>();
        var generatedLazy = _generatedProvider.GetService<Lazy<MyAutomaticService>>();

        Assert.NotNull(reflectionLazy);
        Assert.NotNull(generatedLazy);
        Assert.NotNull(reflectionLazy.Value);
        Assert.NotNull(generatedLazy.Value);
    }

    [Fact]
    public void Parity_RegisteredServiceTypes_SameCount()
    {
        // Get all registered service types from both providers
        var reflectionTypes = GetRegisteredServiceTypes(_reflectionProvider);
        var generatedTypes = GetRegisteredServiceTypes(_generatedProvider);

        // Filter to only include types in our test namespace
        var reflectionTestTypes = reflectionTypes
            .Where(t => t.Namespace?.StartsWith("NexusLabs.Needlr.IntegrationTests") == true)
            .OrderBy(t => t.FullName)
            .ToList();
        var generatedTestTypes = generatedTypes
            .Where(t => t.Namespace?.StartsWith("NexusLabs.Needlr.IntegrationTests") == true)
            .OrderBy(t => t.FullName)
            .ToList();

        Assert.Equal(reflectionTestTypes.Count, generatedTestTypes.Count);
    }

    [Fact]
    public void Parity_RegisteredServiceTypes_SameTypes()
    {
        var reflectionTypes = GetRegisteredServiceTypes(_reflectionProvider);
        var generatedTypes = GetRegisteredServiceTypes(_generatedProvider);

        // Filter to only include types in our test namespace
        var reflectionTestTypes = reflectionTypes
            .Where(t => t.Namespace?.StartsWith("NexusLabs.Needlr.IntegrationTests") == true)
            .OrderBy(t => t.FullName)
            .ToList();
        var generatedTestTypes = generatedTypes
            .Where(t => t.Namespace?.StartsWith("NexusLabs.Needlr.IntegrationTests") == true)
            .OrderBy(t => t.FullName)
            .ToList();

        // Check each type is present in both
        foreach (var reflectionType in reflectionTestTypes)
        {
            Assert.Contains(reflectionType, generatedTestTypes);
        }

        foreach (var generatedType in generatedTestTypes)
        {
            Assert.Contains(generatedType, reflectionTestTypes);
        }
    }

    [Fact]
    public void Parity_ServiceLifetimes_Match()
    {
        var reflectionRegistrations = GetServiceRegistrations(_reflectionProvider);
        var generatedRegistrations = GetServiceRegistrations(_generatedProvider);

        // Filter to test namespace types
        var reflectionTestRegs = reflectionRegistrations
            .Where(r => r.ServiceType.Namespace?.StartsWith("NexusLabs.Needlr.IntegrationTests") == true)
            .ToList();
        var generatedTestRegs = generatedRegistrations
            .Where(r => r.ServiceType.Namespace?.StartsWith("NexusLabs.Needlr.IntegrationTests") == true)
            .ToList();

        // For each registration in reflection, verify same lifetime in generated
        foreach (var reflectionReg in reflectionTestRegs)
        {
            var matchingGeneratedReg = generatedTestRegs
                .FirstOrDefault(g => g.ServiceType == reflectionReg.ServiceType);

            Assert.NotNull(matchingGeneratedReg);
            Assert.Equal(reflectionReg.Lifetime, matchingGeneratedReg.Lifetime);
        }
    }

    [Fact]
    public void Parity_ImplementationTypes_Match()
    {
        var reflectionRegistrations = GetServiceRegistrations(_reflectionProvider);
        var generatedRegistrations = GetServiceRegistrations(_generatedProvider);

        // Filter to test namespace types with concrete implementation types
        var reflectionTestRegs = reflectionRegistrations
            .Where(r => r.ServiceType.Namespace?.StartsWith("NexusLabs.Needlr.IntegrationTests") == true)
            .Where(r => r.ImplementationType != null)
            .ToList();
        var generatedTestRegs = generatedRegistrations
            .Where(r => r.ServiceType.Namespace?.StartsWith("NexusLabs.Needlr.IntegrationTests") == true)
            .Where(r => r.ImplementationType != null)
            .ToList();

        // For each registration with implementation type in reflection, verify match in generated
        foreach (var reflectionReg in reflectionTestRegs)
        {
            var matchingGeneratedReg = generatedTestRegs
                .FirstOrDefault(g => g.ServiceType == reflectionReg.ServiceType);

            Assert.NotNull(matchingGeneratedReg);
            Assert.Equal(reflectionReg.ImplementationType, matchingGeneratedReg.ImplementationType);
        }
    }

    private static IReadOnlySet<Type> GetRegisteredServiceTypes(IServiceProvider provider)
    {
        var descriptors = GetServiceDescriptors(provider);
        return new HashSet<Type>(descriptors.Select(d => d.ServiceType));
    }

    private static IReadOnlyList<ServiceDescriptor> GetServiceRegistrations(IServiceProvider provider)
    {
        return GetServiceDescriptors(provider);
    }

    private static ServiceDescriptor[] GetServiceDescriptors(IServiceProvider provider)
    {
        if (provider is not ServiceProvider sp)
        {
            return Array.Empty<ServiceDescriptor>();
        }

        // Access the service collection through reflection (for testing purposes)
        var rootScope = sp.GetType()
            .GetProperty("RootScope", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .GetValue(sp);

        if (rootScope == null)
        {
            return Array.Empty<ServiceDescriptor>();
        }

        var engine = rootScope.GetType()
            .GetProperty("RootProvider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .GetValue(rootScope);

        if (engine == null)
        {
            return Array.Empty<ServiceDescriptor>();
        }

        var callSiteFactory = engine.GetType()
            .GetField("_callSiteFactory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .GetValue(engine);

        if (callSiteFactory == null)
        {
            return Array.Empty<ServiceDescriptor>();
        }

        var descriptors = callSiteFactory.GetType()
            .GetField("_descriptors", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .GetValue(callSiteFactory);

        if (descriptors is ServiceDescriptor[] descriptorArray)
        {
            return descriptorArray;
        }

        return Array.Empty<ServiceDescriptor>();
    }

    // ========================================================================
    // Plugin Discovery Parity Tests
    // These tests verify that reflection-based and source-generated plugin
    // discovery produce identical results when using REAL generated code.
    // ========================================================================

    /// <summary>
    /// Verifies that both reflection and source-generated plugin factories
    /// discover the same ITestPlugin implementations.
    /// </summary>
    [Fact]
    public void PluginParity_ITestPlugin_BothFactoriesDiscoverSamePlugins()
    {
        // Arrange
        var assemblies = new[] { Assembly.GetExecutingAssembly() };
        var reflectionFactory = new PluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.Generated.TypeRegistry.GetPluginTypes);

        // Act
        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(assemblies)
            .Select(p => p.GetType())
            .OrderBy(t => t.FullName)
            .ToList();

        var generatedPlugins = generatedFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(assemblies)
            .Select(p => p.GetType())
            .OrderBy(t => t.FullName)
            .ToList();

        // Assert - both should discover the same plugin types
        Assert.Equal(reflectionPlugins.Count, generatedPlugins.Count);
        Assert.Equal(reflectionPlugins, generatedPlugins);
    }

    /// <summary>
    /// Verifies that both factories instantiate plugins that are functional.
    /// </summary>
    [Fact]
    public void PluginParity_ITestPlugin_BothFactoriesInstantiateWorkingPlugins()
    {
        // Arrange
        var assemblies = new[] { Assembly.GetExecutingAssembly() };
        var reflectionFactory = new PluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.Generated.TypeRegistry.GetPluginTypes);

        // Act
        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(assemblies)
            .ToList();

        var generatedPlugins = generatedFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(assemblies)
            .ToList();

        // Assert - all plugins should be non-null and have working Name property
        Assert.All(reflectionPlugins, p =>
        {
            Assert.NotNull(p);
            Assert.NotNull(p.Name);
        });

        Assert.All(generatedPlugins, p =>
        {
            Assert.NotNull(p);
            Assert.NotNull(p.Name);
        });

        // Verify the same plugin names
        var reflectionNames = reflectionPlugins.Select(p => p.Name).OrderBy(n => n).ToList();
        var generatedNames = generatedPlugins.Select(p => p.Name).OrderBy(n => n).ToList();
        Assert.Equal(reflectionNames, generatedNames);
    }

    /// <summary>
    /// Verifies that both factories discover multi-interface plugins correctly.
    /// </summary>
    [Fact]
    public void PluginParity_ITestPluginWithOutput_BothFactoriesDiscoverSamePlugins()
    {
        // Arrange
        var assemblies = new[] { Assembly.GetExecutingAssembly() };
        var reflectionFactory = new PluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.Generated.TypeRegistry.GetPluginTypes);

        // Act
        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<ITestPluginWithOutput>(assemblies)
            .ToList();

        var generatedPlugins = generatedFactory
            .CreatePluginsFromAssemblies<ITestPluginWithOutput>(assemblies)
            .ToList();

        // Assert - both should find MultiInterfaceTestPlugin
        Assert.Equal(reflectionPlugins.Count, generatedPlugins.Count);
        Assert.Contains(reflectionPlugins, p => p.GetType() == typeof(MultiInterfaceTestPlugin));
        Assert.Contains(generatedPlugins, p => p.GetType() == typeof(MultiInterfaceTestPlugin));

        // Verify output works
        foreach (var plugin in generatedPlugins)
        {
            Assert.NotNull(plugin.GetOutput());
        }
    }

    /// <summary>
    /// Verifies that both factories exclude plugins without parameterless constructors.
    /// </summary>
    [Fact]
    public void PluginParity_PluginWithDependency_ExcludedByBothFactories()
    {
        // Arrange
        var assemblies = new[] { Assembly.GetExecutingAssembly() };
        var reflectionFactory = new PluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.Generated.TypeRegistry.GetPluginTypes);

        // Act
        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(assemblies)
            .ToList();

        var generatedPlugins = generatedFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(assemblies)
            .ToList();

        // Assert - PluginWithDependency should NOT be in either list
        Assert.DoesNotContain(reflectionPlugins, p => p.GetType() == typeof(PluginWithDependency));
        Assert.DoesNotContain(generatedPlugins, p => p.GetType() == typeof(PluginWithDependency));
    }

    /// <summary>
    /// Verifies that both factories exclude abstract plugins.
    /// </summary>
    [Fact]
    public void PluginParity_AbstractTestPlugin_ExcludedByBothFactories()
    {
        // Arrange
        var assemblies = new[] { Assembly.GetExecutingAssembly() };
        var reflectionFactory = new PluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.Generated.TypeRegistry.GetPluginTypes);

        // Act
        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(assemblies)
            .ToList();

        var generatedPlugins = generatedFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(assemblies)
            .ToList();

        // Assert - AbstractTestPlugin should NOT be in either list
        Assert.DoesNotContain(reflectionPlugins, p => p.GetType() == typeof(AbstractTestPlugin));
        Assert.DoesNotContain(generatedPlugins, p => p.GetType() == typeof(AbstractTestPlugin));

        // But ConcreteTestPlugin SHOULD be in both lists
        Assert.Contains(reflectionPlugins, p => p.GetType() == typeof(ConcreteTestPlugin));
        Assert.Contains(generatedPlugins, p => p.GetType() == typeof(ConcreteTestPlugin));
    }

    /// <summary>
    /// Verifies that ManualRegistrationTestPlugin is discovered by both factories
    /// since DoNotAutoRegister only affects service registration, not plugin discovery.
    /// </summary>
    [Fact]
    public void PluginParity_ManualRegistrationTestPlugin_DiscoveredByBothFactories()
    {
        // Arrange
        var assemblies = new[] { Assembly.GetExecutingAssembly() };
        var reflectionFactory = new PluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.Generated.TypeRegistry.GetPluginTypes);

        // Act
        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(assemblies)
            .ToList();

        var generatedPlugins = generatedFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(assemblies)
            .ToList();

        // Assert - ManualRegistrationTestPlugin SHOULD be in both lists
        // because DoNotAutoRegister is for service registration, not plugin discovery
        Assert.Contains(reflectionPlugins, p => p.GetType() == typeof(ManualRegistrationTestPlugin));
        Assert.Contains(generatedPlugins, p => p.GetType() == typeof(ManualRegistrationTestPlugin));
    }

    /// <summary>
    /// Verifies that both factories return empty when given empty assemblies.
    /// </summary>
    [Fact]
    public void PluginParity_EmptyAssemblies_BothFactoriesReturnEmpty()
    {
        // Arrange
        var assemblies = Array.Empty<Assembly>();
        var reflectionFactory = new PluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.Generated.TypeRegistry.GetPluginTypes);

        // Act
        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(assemblies)
            .ToList();

        var generatedPlugins = generatedFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(assemblies)
            .ToList();

        // Assert
        Assert.Empty(reflectionPlugins);
        Assert.Empty(generatedPlugins);
    }

    /// <summary>
    /// Verifies that both factories return empty when given assemblies without matching plugins.
    /// </summary>
    [Fact]
    public void PluginParity_UnmatchedAssembly_BothFactoriesReturnEmpty()
    {
        // Arrange - use a system assembly that won't have our plugins
        var assemblies = new[] { typeof(object).Assembly };
        var reflectionFactory = new PluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.Generated.TypeRegistry.GetPluginTypes);

        // Act
        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(assemblies)
            .ToList();

        var generatedPlugins = generatedFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(assemblies)
            .ToList();

        // Assert
        Assert.Empty(reflectionPlugins);
        Assert.Empty(generatedPlugins);
    }

    /// <summary>
    /// Verifies that the generated plugin factory uses compile-time factory delegates
    /// instead of Activator.CreateInstance. This is a behavioral test showing the
    /// generated factory produces working instances.
    /// </summary>
    [Fact]
    public void PluginParity_GeneratedFactory_InstantiatesWithoutActivator()
    {
        // Arrange
        var assemblies = new[] { Assembly.GetExecutingAssembly() };
        var generatedFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.Generated.TypeRegistry.GetPluginTypes);

        // Act
        var plugins = generatedFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(assemblies)
            .ToList();

        // Assert - all plugins should be functional
        Assert.NotEmpty(plugins);
        foreach (var plugin in plugins)
        {
            Assert.NotNull(plugin);
            Assert.NotNull(plugin.Name);
            // Execute should not throw
            plugin.Execute();
        }
    }

    /// <summary>
    /// Verifies exact count parity between reflection and source-generated plugin discovery.
    /// This is a comprehensive test that the source generator discovered all plugin types.
    /// </summary>
    [Fact]
    public void PluginParity_PluginCount_IdenticalBetweenReflectionAndGenerated()
    {
        // Arrange
        var assemblies = new[] { Assembly.GetExecutingAssembly() };
        var reflectionFactory = new PluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.Generated.TypeRegistry.GetPluginTypes);

        // Act
        var reflectionCount = reflectionFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(assemblies)
            .Count();

        var generatedCount = generatedFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(assemblies)
            .Count();

        // Assert
        Assert.Equal(reflectionCount, generatedCount);

        // The expected valid plugins are:
        // 1. SimpleTestPlugin (has parameterless ctor)
        // 2. MultiInterfaceTestPlugin (has parameterless ctor)
        // 3. AnotherTestPlugin (has parameterless ctor)
        // 4. ManualRegistrationTestPlugin (has parameterless ctor, DoNotAutoRegister doesn't affect plugins)
        // 5. ConcreteTestPlugin (has parameterless ctor)
        //
        // Excluded:
        // - PluginWithDependency (no parameterless ctor)
        // - AbstractTestPlugin (abstract)
        Assert.Equal(5, reflectionCount);
        Assert.Equal(5, generatedCount);
    }
}
