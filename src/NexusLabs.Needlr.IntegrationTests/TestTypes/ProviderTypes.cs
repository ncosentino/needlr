using NexusLabs.Needlr.Generators;

namespace NexusLabs.Needlr.IntegrationTests.TestTypes;

// Service interfaces for provider testing
public interface IOrderRepository
{
    string GetOrderById(string id);
}

public interface IOrderValidator
{
    bool Validate(string orderId);
}

public interface IOrderNotifier
{
    void Notify(string message);
}

public interface IOptionalLogger
{
    void Log(string message);
}

public interface IEventHandler
{
    void Handle(string eventName);
}

// Service implementations
public class OrderRepository : IOrderRepository
{
    public string GetOrderById(string id) => $"Order-{id}";
}

public class OrderValidator : IOrderValidator
{
    public bool Validate(string orderId) => !string.IsNullOrEmpty(orderId);
}

public class OrderNotifier : IOrderNotifier
{
    public void Notify(string message) { }
}

public class OptionalLogger : IOptionalLogger
{
    public void Log(string message) { }
}

public class EventHandlerA : IEventHandler
{
    public void Handle(string eventName) { }
}

public class EventHandlerB : IEventHandler
{
    public void Handle(string eventName) { }
}

/// <summary>
/// Provider defined via interface mode.
/// The generator creates OrderServicesProvider implementing this interface.
/// </summary>
[Provider]
public interface IOrderServicesProvider
{
    IOrderRepository Repository { get; }
    IOrderValidator Validator { get; }
}

/// <summary>
/// Provider defined via shorthand class mode.
/// The generator creates IInventoryProvider interface and completes this partial class.
/// </summary>
[Provider(typeof(IOrderNotifier))]
public partial class InventoryProvider { }

/// <summary>
/// Provider with multiple services via shorthand mode.
/// </summary>
[Provider(typeof(IOrderRepository), typeof(IOrderValidator))]
public partial class MultiServiceProvider { }

/// <summary>
/// Provider with optional service (nullable).
/// </summary>
[Provider]
public interface IOptionalServicesProvider
{
    IOrderRepository Repository { get; }
    IOptionalLogger? Logger { get; }
}

/// <summary>
/// Provider with collection of services.
/// </summary>
[Provider]
public interface IEventHandlersProvider
{
    IEnumerable<IEventHandler> EventHandlers { get; }
}

/// <summary>
/// Provider that references another provider (nested).
/// </summary>
[Provider]
public interface INestedProvider
{
    IOrderServicesProvider OrderServices { get; }
    IEventHandlersProvider EventHandlers { get; }
}

/// <summary>
/// Provider with mixed property kinds (required, optional, collection).
/// </summary>
[Provider]
public interface IMixedServicesProvider
{
    IOrderRepository Repository { get; }
    IOptionalLogger? OptionalLogger { get; }
    IEnumerable<IEventHandler> Handlers { get; }
}

/// <summary>
/// Shorthand provider with collections.
/// </summary>
[Provider(Collections = new[] { typeof(IEventHandler) })]
public partial class CollectionShorthandProvider { }

/// <summary>
/// Shorthand provider with optional services.
/// </summary>
[Provider(Optional = new[] { typeof(IOptionalLogger) })]
public partial class OptionalShorthandProvider { }

/// <summary>
/// Shorthand provider with factories parameter.
/// Generates a property for ISimpleFactoryServiceFactory.
/// </summary>
[Provider(Factories = new[] { typeof(SimpleFactoryService) })]
public partial class FactoryShorthandProvider { }
