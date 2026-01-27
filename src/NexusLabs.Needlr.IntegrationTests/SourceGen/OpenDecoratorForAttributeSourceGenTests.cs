using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.SourceGen;

/// <summary>
/// Integration tests for [OpenDecoratorFor] attribute using source-generated discovery.
/// These tests verify that open generic decorators are correctly discovered
/// and expanded to decorate all closed implementations at compile time.
/// </summary>
public sealed class OpenDecoratorForAttributeSourceGenTests
{
    [Fact]
    public void OpenDecoratorFor_OrderHandler_DecoratedWithBothDecorators()
    {
        // Arrange & Act
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        // Assert - The decorators should be applied to IOpenGenericHandler<OrderMessage>
        var handler = serviceProvider.GetRequiredService<IOpenGenericHandler<OrderMessage>>();
        Assert.NotNull(handler);

        // Order=1 is Logging, Order=2 is Metrics
        // Chain: Metrics(Logged(OrderHandler(id)))
        var result = handler.Handle(new OrderMessage { OrderId = "123" });
        Assert.Equal("Metrics(Logged(OrderHandler(123)))", result);
    }

    [Fact]
    public void OpenDecoratorFor_PaymentHandler_DecoratedWithBothDecorators()
    {
        // Arrange & Act
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        // Assert - The decorators should also be applied to IOpenGenericHandler<PaymentMessage>
        var handler = serviceProvider.GetRequiredService<IOpenGenericHandler<PaymentMessage>>();
        Assert.NotNull(handler);

        // Same decorator chain applies to PaymentMessage handlers
        var result = handler.Handle(new PaymentMessage { PaymentId = "456" });
        Assert.Equal("Metrics(Logged(PaymentHandler(456)))", result);
    }

    [Fact]
    public void OpenDecoratorFor_BothHandlers_DecoratedIndependently()
    {
        // Arrange & Act
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        // Get both handlers
        var orderHandler = serviceProvider.GetRequiredService<IOpenGenericHandler<OrderMessage>>();
        var paymentHandler = serviceProvider.GetRequiredService<IOpenGenericHandler<PaymentMessage>>();

        // Assert - Both should be independently decorated
        Assert.NotNull(orderHandler);
        Assert.NotNull(paymentHandler);

        // Different types should result in different handler outputs
        var orderResult = orderHandler.Handle(new OrderMessage { OrderId = "O1" });
        var paymentResult = paymentHandler.Handle(new PaymentMessage { PaymentId = "P1" });

        Assert.Contains("OrderHandler(O1)", orderResult);
        Assert.Contains("PaymentHandler(P1)", paymentResult);

        // But both should have the decorator wrapping
        Assert.StartsWith("Metrics(", orderResult);
        Assert.StartsWith("Metrics(", paymentResult);
    }

    [Fact]
    public void OpenDecoratorFor_DecoratorOrder_IsRespected()
    {
        // Arrange & Act
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        var handler = serviceProvider.GetRequiredService<IOpenGenericHandler<OrderMessage>>();
        var result = handler.Handle(new OrderMessage { OrderId = "test" });

        // Assert - Decorators with Order=1 should be applied before Order=2
        // So Logged should be inside Metrics
        Assert.Contains("Logged(OrderHandler", result); // Logging wraps handler
        Assert.StartsWith("Metrics(Logged(", result); // Metrics wraps logging
    }

    [Fact]
    public void OpenDecoratorFor_ResolvingViaEnumerable_ReturnsDecoratedHandler()
    {
        // Arrange & Act
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        // Assert - Even when resolving as IEnumerable, the decorator chain should be intact
        var handlers = serviceProvider.GetServices<IOpenGenericHandler<OrderMessage>>().ToList();
        
        // We should have handlers (the decorated service)
        Assert.NotEmpty(handlers);
        
        // Any handler we get should have the decorators applied
        foreach (var handler in handlers)
        {
            var result = handler.Handle(new OrderMessage { OrderId = "enum" });
            Assert.Contains("Logged(", result);
        }
    }
}
