using Xunit;

namespace NexusLabs.Needlr.Generators.Tests;

/// <summary>
/// Tests that verify the source generator correctly handles [OpenDecoratorFor] attributes.
/// These tests ensure open generic decorators are discovered and expanded into concrete
/// decorator registrations for all discovered closed implementations.
/// </summary>
public sealed class OpenDecoratorForGeneratorTests
{
    [Fact]
    public void OpenDecoratorFor_SingleHandler_GeneratesClosedDecoratorRegistration()
    {
        // Arrange - A generic handler interface, a concrete handler, and an open generic decorator
        var source = """
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TestNamespace" })]

            namespace TestNamespace
            {
                public interface IHandler<T>
                {
                    void Handle(T message);
                }

                [Singleton]
                public class OrderHandler : IHandler<Order>
                {
                    public void Handle(Order message) { }
                }

                public class Order { }

                [OpenDecoratorFor(typeof(IHandler<>))]
                public class LoggingDecorator<T> : IHandler<T>
                {
                    private readonly IHandler<T> _inner;
                    public LoggingDecorator(IHandler<T> inner) => _inner = inner;
                    public void Handle(T message) => _inner.Handle(message);
                }
            }
            """;

        // Act
        var generatedCode = GeneratorTestRunner.ForDecoratorWithInlineTypes()
            .WithSource(source)
            .RunTypeRegistryGenerator();

        // Assert - Should generate a closed decorator for IHandler<Order>
        Assert.Contains("ApplyDecorators", generatedCode);
        Assert.Contains("AddDecorator<global::TestNamespace.IHandler<global::TestNamespace.Order>, global::TestNamespace.LoggingDecorator<global::TestNamespace.Order>>", generatedCode);
    }

    [Fact]
    public void OpenDecoratorFor_MultipleHandlers_GeneratesClosedDecoratorForEach()
    {
        // Arrange - Multiple closed implementations of a generic handler
        var source = """
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TestNamespace" })]

            namespace TestNamespace
            {
                public interface IHandler<T>
                {
                    void Handle(T message);
                }

                [Singleton]
                public class OrderHandler : IHandler<Order>
                {
                    public void Handle(Order message) { }
                }

                [Singleton]
                public class PaymentHandler : IHandler<Payment>
                {
                    public void Handle(Payment message) { }
                }

                public class Order { }
                public class Payment { }

                [OpenDecoratorFor(typeof(IHandler<>))]
                public class LoggingDecorator<T> : IHandler<T>
                {
                    private readonly IHandler<T> _inner;
                    public LoggingDecorator(IHandler<T> inner) => _inner = inner;
                    public void Handle(T message) => _inner.Handle(message);
                }
            }
            """;

        // Act
        var generatedCode = GeneratorTestRunner.ForDecoratorWithInlineTypes()
            .WithSource(source)
            .RunTypeRegistryGenerator();

        // Assert - Should generate closed decorators for both IHandler<Order> and IHandler<Payment>
        Assert.Contains("ApplyDecorators", generatedCode);
        Assert.Contains("LoggingDecorator<global::TestNamespace.Order>", generatedCode);
        Assert.Contains("LoggingDecorator<global::TestNamespace.Payment>", generatedCode);
    }

    [Fact]
    public void OpenDecoratorFor_WithOrder_OrderIsPreserved()
    {
        // Arrange - An open decorator with a specific order
        var source = """
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TestNamespace" })]

            namespace TestNamespace
            {
                public interface IHandler<T>
                {
                    void Handle(T message);
                }

                [Singleton]
                public class OrderHandler : IHandler<Order>
                {
                    public void Handle(Order message) { }
                }

                public class Order { }

                [OpenDecoratorFor(typeof(IHandler<>), Order = 5)]
                public class LoggingDecorator<T> : IHandler<T>
                {
                    private readonly IHandler<T> _inner;
                    public LoggingDecorator(IHandler<T> inner) => _inner = inner;
                    public void Handle(T message) => _inner.Handle(message);
                }
            }
            """;

        // Act
        var generatedCode = GeneratorTestRunner.ForDecoratorWithInlineTypes()
            .WithSource(source)
            .RunTypeRegistryGenerator();

        // Assert - Order should be preserved in the generated code
        Assert.Contains("Order: 5", generatedCode);
    }

    [Fact]
    public void OpenDecoratorFor_MultipleOpenDecorators_AllAreExpanded()
    {
        // Arrange - Multiple open decorators for the same interface
        var source = """
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TestNamespace" })]

            namespace TestNamespace
            {
                public interface IHandler<T>
                {
                    void Handle(T message);
                }

                [Singleton]
                public class OrderHandler : IHandler<Order>
                {
                    public void Handle(Order message) { }
                }

                public class Order { }

                [OpenDecoratorFor(typeof(IHandler<>), Order = 1)]
                public class LoggingDecorator<T> : IHandler<T>
                {
                    private readonly IHandler<T> _inner;
                    public LoggingDecorator(IHandler<T> inner) => _inner = inner;
                    public void Handle(T message) => _inner.Handle(message);
                }

                [OpenDecoratorFor(typeof(IHandler<>), Order = 2)]
                public class MetricsDecorator<T> : IHandler<T>
                {
                    private readonly IHandler<T> _inner;
                    public MetricsDecorator(IHandler<T> inner) => _inner = inner;
                    public void Handle(T message) => _inner.Handle(message);
                }
            }
            """;

        // Act
        var generatedCode = GeneratorTestRunner.ForDecoratorWithInlineTypes()
            .WithSource(source)
            .RunTypeRegistryGenerator();

        // Assert - Both decorators should be expanded
        Assert.Contains("LoggingDecorator<global::TestNamespace.Order>", generatedCode);
        Assert.Contains("MetricsDecorator<global::TestNamespace.Order>", generatedCode);
    }

    [Fact]
    public void OpenDecoratorFor_NoImplementations_NoDecoratorsGenerated()
    {
        // Arrange - An open decorator with no closed implementations to decorate
        var source = """
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TestNamespace" })]

            namespace TestNamespace
            {
                public interface IHandler<T>
                {
                    void Handle(T message);
                }

                public class Order { }

                [OpenDecoratorFor(typeof(IHandler<>))]
                public class LoggingDecorator<T> : IHandler<T>
                {
                    private readonly IHandler<T> _inner;
                    public LoggingDecorator(IHandler<T> inner) => _inner = inner;
                    public void Handle(T message) => _inner.Handle(message);
                }
            }
            """;

        // Act
        var generatedCode = GeneratorTestRunner.ForDecoratorWithInlineTypes()
            .WithSource(source)
            .RunTypeRegistryGenerator();

        // Assert - No LoggingDecorator registrations should be generated
        // (because there are no concrete IHandler<T> implementations)
        Assert.DoesNotContain("LoggingDecorator<global::TestNamespace.Order>", generatedCode);
    }

    [Fact]
    public void OpenDecoratorFor_TwoTypeParameters_ExpandsCorrectly()
    {
        // Arrange - Interface with two type parameters
        var source = """
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TestNamespace" })]

            namespace TestNamespace
            {
                public interface IRequestHandler<TRequest, TResponse>
                {
                    TResponse Handle(TRequest request);
                }

                [Singleton]
                public class GetOrderHandler : IRequestHandler<GetOrderRequest, Order>
                {
                    public Order Handle(GetOrderRequest request) => new Order();
                }

                public class GetOrderRequest { }
                public class Order { }

                [OpenDecoratorFor(typeof(IRequestHandler<,>))]
                public class LoggingDecorator<TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
                {
                    private readonly IRequestHandler<TRequest, TResponse> _inner;
                    public LoggingDecorator(IRequestHandler<TRequest, TResponse> inner) => _inner = inner;
                    public TResponse Handle(TRequest request) => _inner.Handle(request);
                }
            }
            """;

        // Act
        var generatedCode = GeneratorTestRunner.ForDecoratorWithInlineTypes()
            .WithSource(source)
            .RunTypeRegistryGenerator();

        // Assert - Should generate a decorator with both type parameters filled
        Assert.Contains("LoggingDecorator<global::TestNamespace.GetOrderRequest, global::TestNamespace.Order>", generatedCode);
    }
}
