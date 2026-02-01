using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;
using NexusLabs.Needlr.IntegrationTests.TestTypes;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.SourceGen;

/// <summary>
/// Integration tests that verify generated Providers work at runtime.
/// These tests prove that:
/// 1. Providers are registered as singletons
/// 2. Provider properties resolve the correct services
/// 3. Both interface-mode and shorthand class-mode providers work
/// </summary>
public sealed class ProviderSourceGenTests
{
    private static IServiceProvider BuildServiceProvider()
    {
        return new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();
    }

    [Fact]
    public void InterfaceProvider_IsResolvable()
    {
        var provider = BuildServiceProvider();

        var orderServices = provider.GetService<IOrderServicesProvider>();

        Assert.NotNull(orderServices);
    }

    [Fact]
    public void InterfaceProvider_IsRegisteredAsSingleton()
    {
        var provider = BuildServiceProvider();

        var first = provider.GetRequiredService<IOrderServicesProvider>();
        var second = provider.GetRequiredService<IOrderServicesProvider>();

        Assert.Same(first, second);
    }

    [Fact]
    public void InterfaceProvider_RepositoryProperty_ReturnsCorrectService()
    {
        var provider = BuildServiceProvider();

        var orderServices = provider.GetRequiredService<IOrderServicesProvider>();

        Assert.NotNull(orderServices.Repository);
        Assert.IsAssignableFrom<IOrderRepository>(orderServices.Repository);
        Assert.Equal("Order-123", orderServices.Repository.GetOrderById("123"));
    }

    [Fact]
    public void InterfaceProvider_ValidatorProperty_ReturnsCorrectService()
    {
        var provider = BuildServiceProvider();

        var orderServices = provider.GetRequiredService<IOrderServicesProvider>();

        Assert.NotNull(orderServices.Validator);
        Assert.IsAssignableFrom<IOrderValidator>(orderServices.Validator);
        Assert.True(orderServices.Validator.Validate("test"));
        Assert.False(orderServices.Validator.Validate(""));
    }

    [Fact]
    public void InterfaceProvider_MultipleProperties_AllResolved()
    {
        var provider = BuildServiceProvider();

        var orderServices = provider.GetRequiredService<IOrderServicesProvider>();

        Assert.NotNull(orderServices.Repository);
        Assert.NotNull(orderServices.Validator);
    }

    [Fact]
    public void ShorthandProvider_IsResolvableViaInterface()
    {
        var provider = BuildServiceProvider();

        var inventory = provider.GetService<IInventoryProvider>();

        Assert.NotNull(inventory);
    }

    [Fact]
    public void ShorthandProvider_IsRegisteredAsSingleton()
    {
        var provider = BuildServiceProvider();

        var first = provider.GetRequiredService<IInventoryProvider>();
        var second = provider.GetRequiredService<IInventoryProvider>();

        Assert.Same(first, second);
    }

    [Fact]
    public void ShorthandProvider_Property_ReturnsCorrectService()
    {
        var provider = BuildServiceProvider();

        var inventory = provider.GetRequiredService<IInventoryProvider>();

        Assert.NotNull(inventory.OrderNotifier);
        Assert.IsAssignableFrom<IOrderNotifier>(inventory.OrderNotifier);
    }

    [Fact]
    public void MultiServiceProvider_IsResolvable()
    {
        var provider = BuildServiceProvider();

        var multiService = provider.GetService<IMultiServiceProvider>();

        Assert.NotNull(multiService);
    }

    [Fact]
    public void MultiServiceProvider_AllProperties_Resolved()
    {
        var provider = BuildServiceProvider();

        var multiService = provider.GetRequiredService<IMultiServiceProvider>();

        Assert.NotNull(multiService.OrderRepository);
        Assert.NotNull(multiService.OrderValidator);
    }

    [Fact]
    public void MultiServiceProvider_Properties_ReturnWorkingServices()
    {
        var provider = BuildServiceProvider();

        var multiService = provider.GetRequiredService<IMultiServiceProvider>();

        Assert.Equal("Order-456", multiService.OrderRepository.GetOrderById("456"));
        Assert.True(multiService.OrderValidator.Validate("valid"));
    }

    [Fact]
    public void Provider_SameServiceInstance_AcrossMultipleProviders()
    {
        var provider = BuildServiceProvider();

        var orderServices = provider.GetRequiredService<IOrderServicesProvider>();
        var multiService = provider.GetRequiredService<IMultiServiceProvider>();

        // Both providers should get the same IOrderRepository singleton
        Assert.Same(orderServices.Repository, multiService.OrderRepository);
    }

    [Fact]
    public void Provider_ConcreteClass_IsAlsoResolvable()
    {
        var provider = BuildServiceProvider();

        // The partial class itself should be resolvable as its generated interface
        var viaInterface = provider.GetRequiredService<IInventoryProvider>();

        Assert.NotNull(viaInterface);
        Assert.IsType<InventoryProvider>(viaInterface);
    }

    [Fact]
    public void OptionalProvider_WithServiceRegistered_ResolvesService()
    {
        var provider = BuildServiceProvider();

        var optionalServices = provider.GetRequiredService<IOptionalServicesProvider>();

        Assert.NotNull(optionalServices);
        Assert.NotNull(optionalServices.Repository);
        Assert.NotNull(optionalServices.Logger); // Logger is registered
    }

    [Fact]
    public void OptionalProvider_IsRegisteredAsSingleton()
    {
        var provider = BuildServiceProvider();

        var first = provider.GetRequiredService<IOptionalServicesProvider>();
        var second = provider.GetRequiredService<IOptionalServicesProvider>();

        Assert.Same(first, second);
    }

    [Fact]
    public void CollectionProvider_ResolvesAllHandlers()
    {
        var provider = BuildServiceProvider();

        var handlersProvider = provider.GetRequiredService<IEventHandlersProvider>();

        Assert.NotNull(handlersProvider);
        Assert.NotNull(handlersProvider.EventHandlers);
        // Should have both EventHandlerA and EventHandlerB
        var handlers = handlersProvider.EventHandlers.ToList();
        Assert.Equal(2, handlers.Count);
    }

    [Fact]
    public void CollectionProvider_IsRegisteredAsSingleton()
    {
        var provider = BuildServiceProvider();

        var first = provider.GetRequiredService<IEventHandlersProvider>();
        var second = provider.GetRequiredService<IEventHandlersProvider>();

        Assert.Same(first, second);
    }

    [Fact]
    public void NestedProvider_ResolvesChildProviders()
    {
        var provider = BuildServiceProvider();

        var nestedProvider = provider.GetRequiredService<INestedProvider>();

        Assert.NotNull(nestedProvider);
        Assert.NotNull(nestedProvider.OrderServices);
        Assert.NotNull(nestedProvider.EventHandlers);
        // Verify nested providers have their own services
        Assert.NotNull(nestedProvider.OrderServices.Repository);
        Assert.NotNull(nestedProvider.EventHandlers.EventHandlers);
    }

    [Fact]
    public void NestedProvider_ChildProvidersAreSameInstances()
    {
        var provider = BuildServiceProvider();

        var nestedProvider = provider.GetRequiredService<INestedProvider>();
        var directOrderServices = provider.GetRequiredService<IOrderServicesProvider>();
        var directEventHandlers = provider.GetRequiredService<IEventHandlersProvider>();

        // Nested providers should be the same singleton instances
        Assert.Same(nestedProvider.OrderServices, directOrderServices);
        Assert.Same(nestedProvider.EventHandlers, directEventHandlers);
    }

    [Fact]
    public void MixedProvider_ResolvesAllPropertyKinds()
    {
        var provider = BuildServiceProvider();

        var mixedProvider = provider.GetRequiredService<IMixedServicesProvider>();

        Assert.NotNull(mixedProvider);
        // Required
        Assert.NotNull(mixedProvider.Repository);
        // Optional
        Assert.NotNull(mixedProvider.OptionalLogger);
        // Collection
        Assert.NotNull(mixedProvider.Handlers);
        Assert.Equal(2, mixedProvider.Handlers.Count());
    }

    [Fact]
    public void ShorthandCollectionProvider_IsResolvable()
    {
        var provider = BuildServiceProvider();

        var collectionProvider = provider.GetService<ICollectionShorthandProvider>();

        Assert.NotNull(collectionProvider);
    }

    [Fact]
    public void ShorthandCollectionProvider_ResolvesHandlers()
    {
        var provider = BuildServiceProvider();

        var collectionProvider = provider.GetRequiredService<ICollectionShorthandProvider>();

        Assert.NotNull(collectionProvider.EventHandlers);
        Assert.Equal(2, collectionProvider.EventHandlers.Count());
    }

    [Fact]
    public void ShorthandOptionalProvider_IsResolvable()
    {
        var provider = BuildServiceProvider();

        var optionalProvider = provider.GetService<IOptionalShorthandProvider>();

        Assert.NotNull(optionalProvider);
    }

    [Fact]
    public void ShorthandOptionalProvider_ResolvesOptionalService()
    {
        var provider = BuildServiceProvider();

        var optionalProvider = provider.GetRequiredService<IOptionalShorthandProvider>();

        // OptionalLogger is registered, so it should be resolved
        Assert.NotNull(optionalProvider.OptionalLogger);
    }
}
