using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Extensions.Configuration;
using NexusLabs.Needlr.Injection;

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
}
