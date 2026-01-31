namespace NexusLabs.Needlr.IntegrationTests;

/// <summary>
/// Open generic interface for testing [OpenDecoratorFor] attribute.
/// </summary>
public interface IOpenGenericHandler<T>
{
    string Handle(T message);
}

/// <summary>
/// Message type for testing open generic decorators.
/// </summary>
public sealed class OrderMessage
{
    public string OrderId { get; init; } = string.Empty;
}

/// <summary>
/// Message type for testing open generic decorators.
/// </summary>
public sealed class PaymentMessage
{
    public string PaymentId { get; init; } = string.Empty;
}

/// <summary>
/// Concrete implementation of IOpenGenericHandler for OrderMessage.
/// </summary>
[Singleton]
public sealed class OrderMessageHandler : IOpenGenericHandler<OrderMessage>
{
    public string Handle(OrderMessage message) => $"OrderHandler({message.OrderId})";
}

/// <summary>
/// Concrete implementation of IOpenGenericHandler for PaymentMessage.
/// </summary>
[Singleton]
public sealed class PaymentMessageHandler : IOpenGenericHandler<PaymentMessage>
{
    public string Handle(PaymentMessage message) => $"PaymentHandler({message.PaymentId})";
}

/// <summary>
/// Open generic decorator that decorates all IOpenGenericHandler implementations.
/// Uses [OpenDecoratorFor] to automatically decorate IHandler{OrderMessage} and IHandler{PaymentMessage}.
/// </summary>
[Generators.OpenDecoratorFor(typeof(IOpenGenericHandler<>), Order = 1)]
public sealed class LoggingOpenDecorator<T> : IOpenGenericHandler<T>
{
    private readonly IOpenGenericHandler<T> _inner;

    public LoggingOpenDecorator(IOpenGenericHandler<T> inner)
    {
        _inner = inner;
    }

    public string Handle(T message) => $"Logged({_inner.Handle(message)})";
}

/// <summary>
/// Second open generic decorator with higher order (wraps LoggingOpenDecorator).
/// </summary>
[Generators.OpenDecoratorFor(typeof(IOpenGenericHandler<>), Order = 2)]
public sealed class MetricsOpenDecorator<T> : IOpenGenericHandler<T>
{
    private readonly IOpenGenericHandler<T> _inner;

    public MetricsOpenDecorator(IOpenGenericHandler<T> inner)
    {
        _inner = inner;
    }

    public string Handle(T message) => $"Metrics({_inner.Handle(message)})";
}
