using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

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
        var generatedCode = RunGenerator(source);

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
        var generatedCode = RunGenerator(source);

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
        var generatedCode = RunGenerator(source);

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
        var generatedCode = RunGenerator(source);

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
        var generatedCode = RunGenerator(source);

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
        var generatedCode = RunGenerator(source);

        // Assert - Should generate a decorator with both type parameters filled
        Assert.Contains("LoggingDecorator<global::TestNamespace.GetOrderRequest, global::TestNamespace.Order>", generatedCode);
    }

    private static string RunGenerator(string source)
    {
        // Create the attribute source with all required types
        var attributeSource = """
            namespace NexusLabs.Needlr
            {
                [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
                public sealed class DoNotAutoRegisterAttribute : System.Attribute { }

                [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
                public sealed class SingletonAttribute : System.Attribute { }

                [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
                public sealed class DecoratorForAttribute<TService> : System.Attribute
                    where TService : class
                {
                    public int Order { get; set; } = 0;
                    public System.Type ServiceType => typeof(TService);
                }
            }

            namespace NexusLabs.Needlr.Generators
            {
                [System.AttributeUsage(System.AttributeTargets.Assembly)]
                public sealed class GenerateTypeRegistryAttribute : System.Attribute
                {
                    public string[]? IncludeNamespacePrefixes { get; set; }
                    public bool IncludeSelf { get; set; } = true;
                }

                [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
                public sealed class OpenDecoratorForAttribute : System.Attribute
                {
                    public OpenDecoratorForAttribute(System.Type openGenericServiceType)
                    {
                        OpenGenericServiceType = openGenericServiceType;
                    }

                    public System.Type OpenGenericServiceType { get; }
                    public int Order { get; set; } = 0;
                }

                public enum InjectableLifetime
                {
                    Singleton = 0,
                    Scoped = 1,
                    Transient = 2
                }

                public readonly struct InjectableTypeInfo
                {
                    public InjectableTypeInfo(System.Type type, System.Collections.Generic.IReadOnlyList<System.Type> interfaces)
                        : this(type, interfaces, null) { }

                    public InjectableTypeInfo(System.Type type, System.Collections.Generic.IReadOnlyList<System.Type> interfaces, InjectableLifetime? lifetime)
                    {
                        Type = type;
                        Interfaces = interfaces;
                        Lifetime = lifetime;
                    }

                    public System.Type Type { get; }
                    public System.Collections.Generic.IReadOnlyList<System.Type> Interfaces { get; }
                    public InjectableLifetime? Lifetime { get; }
                }

                public readonly struct PluginTypeInfo
                {
                    public PluginTypeInfo(System.Type pluginType, System.Collections.Generic.IReadOnlyList<System.Type> pluginInterfaces, System.Func<object> factory)
                    {
                        PluginType = pluginType;
                        PluginInterfaces = pluginInterfaces;
                        Factory = factory;
                    }

                    public System.Type PluginType { get; }
                    public System.Collections.Generic.IReadOnlyList<System.Type> PluginInterfaces { get; }
                    public System.Func<object> Factory { get; }
                }

                public static class NeedlrSourceGenBootstrap
                {
                    public static void Register(
                        System.Func<System.Collections.Generic.IReadOnlyList<InjectableTypeInfo>> injectableTypeProvider,
                        System.Func<System.Collections.Generic.IReadOnlyList<PluginTypeInfo>> pluginTypeProvider,
                        System.Action<object>? decoratorApplier = null)
                    {
                    }
                }
            }
            """;

        var syntaxTrees = new[]
        {
            CSharpSyntaxTree.ParseText(attributeSource),
            CSharpSyntaxTree.ParseText(source)
        };

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            syntaxTrees,
            Basic.Reference.Assemblies.Net100.References.All,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new TypeRegistryGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var diagnostics);

        var generatedTrees = outputCompilation.SyntaxTrees
            .Where(t => t.FilePath.EndsWith(".g.cs"))
            .OrderBy(t => t.FilePath)
            .ToList();

        if (generatedTrees.Count == 0)
        {
            return string.Empty;
        }

        return string.Join("\n\n", generatedTrees.Select(t => t.GetText().ToString()));
    }
}
