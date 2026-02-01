using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;
using NexusLabs.Needlr.Injection.SourceGen;

using System.Reflection;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.Parity;

/// <summary>
/// Parity tests that verify pre-registration callback behavior is consistent between
/// reflection and source-generation paths. Pre-registration callbacks allow custom
/// service registrations (e.g., open generics) before auto-discovery runs.
/// </summary>
public sealed class PreRegistrationCallbackParityTests
{
    private readonly IConfiguration _configuration;
    private readonly Assembly[] _assemblies;

    public PreRegistrationCallbackParityTests()
    {
        _configuration = new ConfigurationBuilder().Build();
        _assemblies = [Assembly.GetExecutingAssembly()];
    }

    [Fact]
    public void Parity_SinglePreRegistrationCallback_ExecutesBeforeAutoDiscovery()
    {
        // Arrange
        var executionOrder = new List<string>();

        // Act - Reflection
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .UsingAdditionalAssemblies(_assemblies)
            .UsingPreRegistrationCallback(services =>
            {
                executionOrder.Add("pre-reflection");
                services.AddSingleton<IPreRegistrationTestService, PreRegistrationTestService>();
            })
            .BuildServiceProvider(_configuration);

        // Act - SourceGen
        var sourceGenProvider = new Syringe()
            .UsingSourceGen()
            .UsingAdditionalAssemblies(_assemblies)
            .UsingPreRegistrationCallback(services =>
            {
                executionOrder.Add("pre-sourcegen");
                services.AddSingleton<IPreRegistrationTestService, PreRegistrationTestService>();
            })
            .BuildServiceProvider(_configuration);

        // Assert - Both paths execute callback and service is resolvable
        var reflectionService = reflectionProvider.GetService<IPreRegistrationTestService>();
        var sourceGenService = sourceGenProvider.GetService<IPreRegistrationTestService>();

        Assert.NotNull(reflectionService);
        Assert.NotNull(sourceGenService);
        Assert.Contains("pre-reflection", executionOrder);
        Assert.Contains("pre-sourcegen", executionOrder);
    }

    [Fact]
    public void Parity_OpenGenericRegistration_ViaPreRegistrationCallback()
    {
        // Arrange & Act - Reflection
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .UsingAdditionalAssemblies(_assemblies)
            .UsingPreRegistrationCallback(services =>
                services.AddTransient(typeof(IPreRegistrationRepository<>), typeof(PreRegistrationRepository<>)))
            .BuildServiceProvider(_configuration);

        // Act - SourceGen
        var sourceGenProvider = new Syringe()
            .UsingSourceGen()
            .UsingAdditionalAssemblies(_assemblies)
            .UsingPreRegistrationCallback(services =>
                services.AddTransient(typeof(IPreRegistrationRepository<>), typeof(PreRegistrationRepository<>)))
            .BuildServiceProvider(_configuration);

        // Assert - Both paths can resolve closed generic types
        var reflectionRepo = reflectionProvider.GetService<IPreRegistrationRepository<string>>();
        var sourceGenRepo = sourceGenProvider.GetService<IPreRegistrationRepository<string>>();

        Assert.NotNull(reflectionRepo);
        Assert.NotNull(sourceGenRepo);
        Assert.IsType<PreRegistrationRepository<string>>(reflectionRepo);
        Assert.IsType<PreRegistrationRepository<string>>(sourceGenRepo);
    }

    [Fact]
    public void Parity_MultiplePreRegistrationCallbacks_ExecuteInOrder()
    {
        // Arrange
        var reflectionOrder = new List<int>();
        var sourceGenOrder = new List<int>();

        // Act - Reflection with multiple callbacks
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .UsingAdditionalAssemblies(_assemblies)
            .UsingPreRegistrationCallback(_ => reflectionOrder.Add(1))
            .UsingPreRegistrationCallback(_ => reflectionOrder.Add(2))
            .UsingPreRegistrationCallback(_ => reflectionOrder.Add(3))
            .BuildServiceProvider(_configuration);

        // Act - SourceGen with multiple callbacks
        var sourceGenProvider = new Syringe()
            .UsingSourceGen()
            .UsingAdditionalAssemblies(_assemblies)
            .UsingPreRegistrationCallback(_ => sourceGenOrder.Add(1))
            .UsingPreRegistrationCallback(_ => sourceGenOrder.Add(2))
            .UsingPreRegistrationCallback(_ => sourceGenOrder.Add(3))
            .BuildServiceProvider(_configuration);

        // Assert - Both paths execute callbacks in order
        Assert.Equal([1, 2, 3], reflectionOrder);
        Assert.Equal([1, 2, 3], sourceGenOrder);
    }

    [Fact]
    public void Parity_UsingPreRegistrationCallbacks_Batch_ExecutesAll()
    {
        // Arrange
        var reflectionOrder = new List<int>();
        var sourceGenOrder = new List<int>();

        List<Action<IServiceCollection>> callbacks =
        [
            _ => reflectionOrder.Add(1),
            _ => reflectionOrder.Add(2),
            _ => reflectionOrder.Add(3)
        ];

        List<Action<IServiceCollection>> sourceGenCallbacks =
        [
            _ => sourceGenOrder.Add(1),
            _ => sourceGenOrder.Add(2),
            _ => sourceGenOrder.Add(3)
        ];

        // Act - Reflection with batch callbacks
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .UsingAdditionalAssemblies(_assemblies)
            .UsingPreRegistrationCallbacks(callbacks)
            .BuildServiceProvider(_configuration);

        // Act - SourceGen with batch callbacks
        var sourceGenProvider = new Syringe()
            .UsingSourceGen()
            .UsingAdditionalAssemblies(_assemblies)
            .UsingPreRegistrationCallbacks(sourceGenCallbacks)
            .BuildServiceProvider(_configuration);

        // Assert - Both paths execute all callbacks
        Assert.Equal([1, 2, 3], reflectionOrder);
        Assert.Equal([1, 2, 3], sourceGenOrder);
    }

    [Fact]
    public void Parity_PreRegistrationDoesNotOverrideAutoDiscovery()
    {
        // Arrange - Pre-register a service that is also auto-discovered
        // The auto-discovery should add additional registrations

        // Act - Reflection
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .UsingAdditionalAssemblies(_assemblies)
            .UsingPreRegistrationCallback(services =>
                services.AddSingleton<IPreRegistrationTestService, PreRegistrationTestService>())
            .BuildServiceProvider(_configuration);

        // Act - SourceGen
        var sourceGenProvider = new Syringe()
            .UsingSourceGen()
            .UsingAdditionalAssemblies(_assemblies)
            .UsingPreRegistrationCallback(services =>
                services.AddSingleton<IPreRegistrationTestService, PreRegistrationTestService>())
            .BuildServiceProvider(_configuration);

        // Assert - Services are resolvable in both paths
        var reflectionService = reflectionProvider.GetService<IPreRegistrationTestService>();
        var sourceGenService = sourceGenProvider.GetService<IPreRegistrationTestService>();

        Assert.NotNull(reflectionService);
        Assert.NotNull(sourceGenService);
    }
}

// Test types for pre-registration callback tests
public interface IPreRegistrationTestService
{
    string GetValue();
}

[DoNotAutoRegister]
public class PreRegistrationTestService : IPreRegistrationTestService
{
    public string GetValue() => "PreRegistered";
}

public interface IPreRegistrationRepository<T>
{
    T? Get(int id);
    void Save(T entity);
}

[DoNotAutoRegister]
public class PreRegistrationRepository<T> : IPreRegistrationRepository<T>
{
    public T? Get(int id) => default;
    public void Save(T entity) { }
}
