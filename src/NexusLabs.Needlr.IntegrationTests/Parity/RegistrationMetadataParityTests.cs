using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;
using NexusLabs.Needlr.Injection.SourceGen;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.Parity;

public sealed class RegistrationMetadataParityTests
{
    private readonly IServiceProvider _reflectionProvider;
    private readonly IServiceProvider _generatedProvider;

    public RegistrationMetadataParityTests()
    {
        _reflectionProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        _generatedProvider = new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
            .BuildServiceProvider();
    }

    [Fact]
    public void Parity_RegisteredServiceTypes_SameCount()
    {
        var reflectionTypes = GetRegisteredServiceTypes(_reflectionProvider);
        var generatedTypes = GetRegisteredServiceTypes(_generatedProvider);

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

        var reflectionTestTypes = reflectionTypes
            .Where(t => t.Namespace?.StartsWith("NexusLabs.Needlr.IntegrationTests") == true)
            .OrderBy(t => t.FullName)
            .ToList();
        var generatedTestTypes = generatedTypes
            .Where(t => t.Namespace?.StartsWith("NexusLabs.Needlr.IntegrationTests") == true)
            .OrderBy(t => t.FullName)
            .ToList();

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

        var reflectionTestRegs = reflectionRegistrations
            .Where(r => r.ServiceType.Namespace?.StartsWith("NexusLabs.Needlr.IntegrationTests") == true)
            .ToList();
        var generatedTestRegs = generatedRegistrations
            .Where(r => r.ServiceType.Namespace?.StartsWith("NexusLabs.Needlr.IntegrationTests") == true)
            .ToList();

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

        var reflectionTestRegs = reflectionRegistrations
            .Where(r => r.ServiceType.Namespace?.StartsWith("NexusLabs.Needlr.IntegrationTests") == true)
            .Where(r => r.ImplementationType != null)
            .ToList();
        var generatedTestRegs = generatedRegistrations
            .Where(r => r.ServiceType.Namespace?.StartsWith("NexusLabs.Needlr.IntegrationTests") == true)
            .Where(r => r.ImplementationType != null)
            .ToList();

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
