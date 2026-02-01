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
