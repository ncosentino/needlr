using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace NexusLabs.Needlr.Tests;

/// <summary>
/// Tests for IsRegistered and GetRegisteredTypes/GetRegisteredTypesOf extension methods on IServiceProvider.
/// </summary>
public sealed class ServiceProviderExtensionsIsRegisteredTests
{
    private static IServiceProvider CreateServiceProviderWithServiceCollection(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        configure?.Invoke(services);
        services.AddSingleton<IServiceCollection>(services);
        return services.BuildServiceProvider();
    }

    #region IsRegistered<T> Tests

    [Fact]
    public void IsRegistered_Generic_WithRegisteredService_ReturnsTrue()
    {
        var provider = CreateServiceProviderWithServiceCollection(s =>
            s.AddSingleton<ITestService, TestService>());

        var result = provider.IsRegistered<ITestService>();

        Assert.True(result);
    }

    [Fact]
    public void IsRegistered_Generic_WithUnregisteredService_ReturnsFalse()
    {
        var provider = CreateServiceProviderWithServiceCollection(s =>
            s.AddSingleton<ITestService, TestService>());

        var result = provider.IsRegistered<IOtherService>();

        Assert.False(result);
    }

    [Fact]
    public void IsRegistered_Generic_WithNullServiceProvider_ThrowsArgumentNullException()
    {
        IServiceProvider provider = null!;

        Assert.Throws<ArgumentNullException>(() => provider.IsRegistered<ITestService>());
    }

    [Fact]
    public void IsRegistered_Generic_WithEmptyProvider_ReturnsFalse()
    {
        var provider = CreateServiceProviderWithServiceCollection();

        var result = provider.IsRegistered<ITestService>();

        Assert.False(result);
    }

    #endregion

    #region IsRegistered(Type) Tests

    [Fact]
    public void IsRegistered_NonGeneric_WithRegisteredService_ReturnsTrue()
    {
        var provider = CreateServiceProviderWithServiceCollection(s =>
            s.AddSingleton<ITestService, TestService>());

        var result = provider.IsRegistered(typeof(ITestService));

        Assert.True(result);
    }

    [Fact]
    public void IsRegistered_NonGeneric_WithUnregisteredService_ReturnsFalse()
    {
        var provider = CreateServiceProviderWithServiceCollection(s =>
            s.AddSingleton<ITestService, TestService>());

        var result = provider.IsRegistered(typeof(IOtherService));

        Assert.False(result);
    }

    [Fact]
    public void IsRegistered_NonGeneric_WithNullServiceProvider_ThrowsArgumentNullException()
    {
        IServiceProvider provider = null!;

        Assert.Throws<ArgumentNullException>(() => provider.IsRegistered(typeof(ITestService)));
    }

    #endregion

    #region GetRegisteredTypes Tests

    [Fact]
    public void GetRegisteredTypes_WithInterfaceFilter_ReturnsOnlyInterfaces()
    {
        var provider = CreateServiceProviderWithServiceCollection(s =>
        {
            s.AddSingleton<ITestService, TestService>();
            s.AddSingleton<TestService>();
        });

        var interfaceTypes = provider.GetRegisteredTypes(t => t.IsInterface).ToList();

        Assert.Contains(typeof(ITestService), interfaceTypes);
        Assert.DoesNotContain(typeof(TestService), interfaceTypes);
    }

    [Fact]
    public void GetRegisteredTypes_WithNamespaceFilter_ReturnsMatchingTypes()
    {
        var provider = CreateServiceProviderWithServiceCollection(s =>
        {
            s.AddSingleton<ITestService, TestService>();
        });

        var typesInNamespace = provider.GetRegisteredTypes(t =>
            t.Namespace?.StartsWith("NexusLabs.Needlr.Tests") == true).ToList();

        Assert.Contains(typeof(ITestService), typesInNamespace);
    }

    [Fact]
    public void GetRegisteredTypes_DeduplicatesMultipleRegistrationsOfSameType()
    {
        var provider = CreateServiceProviderWithServiceCollection(s =>
        {
            s.AddSingleton<ITestService, TestService>();
            s.AddSingleton<ITestService, AlternativeTestService>();
        });

        var types = provider.GetRegisteredTypes(t => t == typeof(ITestService)).ToList();

        Assert.Single(types); // Should be deduplicated
    }

    [Fact]
    public void GetRegisteredTypes_WithNoMatches_ReturnsEmpty()
    {
        var provider = CreateServiceProviderWithServiceCollection(s =>
        {
            s.AddSingleton<ITestService, TestService>();
        });

        var types = provider.GetRegisteredTypes(t => t.Namespace == "NonExistent").ToList();

        Assert.Empty(types);
    }

    #endregion

    #region GetRegisteredTypesOf<T> Tests

    [Fact]
    public void GetRegisteredTypesOf_WithMatchingTypes_ReturnsAssignableTypes()
    {
        var provider = CreateServiceProviderWithServiceCollection(s =>
        {
            s.AddSingleton<ITestService, TestService>();
            s.AddSingleton<IOtherService, OtherService>();
        });

        var testServiceTypes = provider.GetRegisteredTypesOf<ITestService>().ToList();

        Assert.Contains(typeof(ITestService), testServiceTypes);
        Assert.DoesNotContain(typeof(IOtherService), testServiceTypes);
    }

    [Fact]
    public void GetRegisteredTypesOf_WithNoMatchingTypes_ReturnsEmpty()
    {
        var provider = CreateServiceProviderWithServiceCollection(s =>
        {
            s.AddSingleton<IOtherService, OtherService>();
        });

        var testServiceTypes = provider.GetRegisteredTypesOf<ITestService>().ToList();

        Assert.Empty(testServiceTypes);
    }

    [Fact]
    public void GetRegisteredTypesOf_WithBaseClass_ReturnsInheritedTypes()
    {
        var provider = CreateServiceProviderWithServiceCollection(s =>
        {
            s.AddSingleton<BaseService>();
            s.AddSingleton<DerivedService>();
        });

        // BaseService is assignable from BaseService and DerivedService
        var baseServiceTypes = provider.GetRegisteredTypesOf<BaseService>().ToList();

        // The registered service types are BaseService and DerivedService
        // Both should be assignable to BaseService
        Assert.Contains(typeof(BaseService), baseServiceTypes);
        Assert.Contains(typeof(DerivedService), baseServiceTypes);
    }

    #endregion

    #region Test Types

    public interface ITestService { }
    public interface IOtherService { }

    [DoNotAutoRegister]
    public class TestService : ITestService { }

    [DoNotAutoRegister]
    public class AlternativeTestService : ITestService { }

    [DoNotAutoRegister]
    public class OtherService : IOtherService { }

    [DoNotAutoRegister]
    public class BaseService { }

    [DoNotAutoRegister]
    public class DerivedService : BaseService { }

    #endregion
}
