using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace NexusLabs.Needlr.Tests;

public sealed class ServiceProviderExtensionTests
{
    private readonly IServiceProvider _serviceProvider;

    public ServiceProviderExtensionTests()
    {
        _serviceProvider = new ServiceCollection().BuildServiceProvider();
    }

    [Fact]
    public void GetRegisteredTypes_WithNullServiceProvider_ThrowsArgumentNullException()
    {
        IServiceProvider nullProvider = null!;

        Assert.Throws<ArgumentNullException>(() =>
            nullProvider.GetRegisteredTypes(type => true));
    }

    [Fact]
    public void GetRegisteredTypes_WithNullPredicate_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _serviceProvider.GetRegisteredTypes(null!));
    }

    [Fact]
    public void GetRegisteredTypesOf_WithNullServiceProvider_ThrowsArgumentNullException()
    {
        IServiceProvider nullProvider = null!;

        Assert.Throws<ArgumentNullException>(() =>
            nullProvider.GetRegisteredTypesOf<object>());
    }

    [Fact]
    public void GetServiceRegistrations_WithNullServiceProvider_ThrowsArgumentNullException()
    {
        IServiceProvider nullProvider = null!;

        Assert.Throws<ArgumentNullException>(() =>
            nullProvider.GetServiceRegistrations(descriptor => true));
    }

    [Fact]
    public void GetServiceRegistrations_WithNullPredicate_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _serviceProvider.GetServiceRegistrations(null!));
    }

    [Fact]
    public void GetRegisteredTypes_WithServiceProviderWithoutServiceCollection_ThrowsInvalidOperationException()
    {
        // Create a minimal service provider without IServiceCollection registered
        var services = new ServiceCollection();
        services.AddSingleton<string>("test");
        var providerWithoutCollection = services.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            providerWithoutCollection.GetRegisteredTypes(type => true));

        Assert.Contains("Unable to access the service collection", exception.Message);
        Assert.Contains("Ensure the IServiceCollection is registered", exception.Message);
    }

    [Fact]
    public void GetServiceRegistrations_WithServiceProviderWithoutServiceCollection_ThrowsInvalidOperationException()
    {
        // Create a minimal service provider without IServiceCollection registered
        var services = new ServiceCollection();
        services.AddSingleton<string>("test");
        var providerWithoutCollection = services.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            providerWithoutCollection.GetServiceRegistrations(descriptor => true));

        Assert.Contains("Unable to access the service collection", exception.Message);
        Assert.Contains("Ensure the IServiceCollection is registered", exception.Message);
    }
}
