using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests;

/// <summary>
/// Tests that verify the source generator correctly handles [Intercept] attributes
/// and generates interceptor proxy classes.
/// </summary>
public sealed class InterceptorProxyGeneratorTests
{
    [Fact]
    public void InterceptAttribute_ClassLevel_GeneratesProxyClass()
    {
        // Arrange
        var source = """
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TestNamespace" })]

            namespace TestNamespace
            {
                public interface IOrderService
                {
                    string GetOrder(int id);
                }

                public class LoggingInterceptor : IMethodInterceptor
                {
                    public System.Threading.Tasks.ValueTask<object?> InterceptAsync(IMethodInvocation invocation)
                    {
                        return invocation.ProceedAsync();
                    }
                }

                [Intercept<LoggingInterceptor>]
                [Scoped]
                public class OrderService : IOrderService
                {
                    public string GetOrder(int id) => $"Order {id}";
                }
            }
            """;

        // Act
        var generatedCode = RunGenerator(source);

        // Assert - Proxy class should be generated
        Assert.Contains("OrderService_InterceptorProxy", generatedCode);
        Assert.Contains("IOrderService", generatedCode);
        Assert.Contains("InterceptorRegistrations", generatedCode);
        Assert.Contains("RegisterInterceptedServices", generatedCode);
    }

    [Fact]
    public void InterceptAttribute_MultipleInterceptors_GeneratesWithOrder()
    {
        // Arrange
        var source = """
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TestNamespace" })]

            namespace TestNamespace
            {
                public interface IOrderService
                {
                    string GetOrder(int id);
                }

                public class LoggingInterceptor : IMethodInterceptor
                {
                    public System.Threading.Tasks.ValueTask<object?> InterceptAsync(IMethodInvocation invocation)
                    {
                        return invocation.ProceedAsync();
                    }
                }

                public class CachingInterceptor : IMethodInterceptor
                {
                    public System.Threading.Tasks.ValueTask<object?> InterceptAsync(IMethodInvocation invocation)
                    {
                        return invocation.ProceedAsync();
                    }
                }

                [Intercept<LoggingInterceptor>(Order = 1)]
                [Intercept<CachingInterceptor>(Order = 2)]
                [Scoped]
                public class OrderService : IOrderService
                {
                    public string GetOrder(int id) => $"Order {id}";
                }
            }
            """;

        // Act
        var generatedCode = RunGenerator(source);

        // Assert - Both interceptors should be registered
        Assert.Contains("LoggingInterceptor", generatedCode);
        Assert.Contains("CachingInterceptor", generatedCode);
        Assert.Contains("OrderService_InterceptorProxy", generatedCode);
    }

    [Fact]
    public void InterceptAttribute_AsyncMethod_GeneratesCorrectProxy()
    {
        // Arrange
        var source = """
            using System.Threading.Tasks;
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TestNamespace" })]

            namespace TestNamespace
            {
                public interface IOrderService
                {
                    Task<string> GetOrderAsync(int id);
                }

                public class LoggingInterceptor : IMethodInterceptor
                {
                    public System.Threading.Tasks.ValueTask<object?> InterceptAsync(IMethodInvocation invocation)
                    {
                        return invocation.ProceedAsync();
                    }
                }

                [Intercept<LoggingInterceptor>]
                [Scoped]
                public class OrderService : IOrderService
                {
                    public Task<string> GetOrderAsync(int id) => Task.FromResult($"Order {id}");
                }
            }
            """;

        // Act
        var generatedCode = RunGenerator(source);

        // Assert - Async method should be handled correctly
        Assert.Contains("GetOrderAsync", generatedCode);
        Assert.Contains("OrderService_InterceptorProxy", generatedCode);
    }

    [Fact]
    public void InterceptAttribute_NonGeneric_GeneratesProxy()
    {
        // Arrange
        var source = """
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TestNamespace" })]

            namespace TestNamespace
            {
                public interface IOrderService
                {
                    string GetOrder(int id);
                }

                public class LoggingInterceptor : IMethodInterceptor
                {
                    public System.Threading.Tasks.ValueTask<object?> InterceptAsync(IMethodInvocation invocation)
                    {
                        return invocation.ProceedAsync();
                    }
                }

                [Intercept(typeof(LoggingInterceptor))]
                [Scoped]
                public class OrderService : IOrderService
                {
                    public string GetOrder(int id) => $"Order {id}";
                }
            }
            """;

        // Act
        var generatedCode = RunGenerator(source);

        // Assert
        Assert.Contains("OrderService_InterceptorProxy", generatedCode);
        Assert.Contains("LoggingInterceptor", generatedCode);
    }

    [Fact]
    public void InterceptAttribute_NoInterceptors_DoesNotGenerateProxyFile()
    {
        // Arrange
        var source = """
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TestNamespace" })]

            namespace TestNamespace
            {
                public interface IOrderService
                {
                    string GetOrder(int id);
                }

                [Scoped]
                public class OrderService : IOrderService
                {
                    public string GetOrder(int id) => $"Order {id}";
                }
            }
            """;

        // Act
        var generatedCode = RunGenerator(source);

        // Assert - No interceptor proxies file should be generated
        Assert.DoesNotContain("InterceptorProxies.g.cs", generatedCode);
        Assert.DoesNotContain("InterceptorRegistrations", generatedCode);
    }

    [Fact]
    public void InterceptAttribute_VoidMethod_GeneratesCorrectProxy()
    {
        // Arrange
        var source = """
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TestNamespace" })]

            namespace TestNamespace
            {
                public interface IOrderService
                {
                    void ProcessOrder(int id);
                }

                public class LoggingInterceptor : IMethodInterceptor
                {
                    public System.Threading.Tasks.ValueTask<object?> InterceptAsync(IMethodInvocation invocation)
                    {
                        return invocation.ProceedAsync();
                    }
                }

                [Intercept<LoggingInterceptor>]
                [Scoped]
                public class OrderService : IOrderService
                {
                    public void ProcessOrder(int id) { }
                }
            }
            """;

        // Act
        var generatedCode = RunGenerator(source);

        // Assert
        Assert.Contains("ProcessOrder", generatedCode);
        Assert.Contains("void", generatedCode);
    }

    private static string RunGenerator(string source)
    {
        // Create the attribute source with all required types including interceptor types
        var attributeSource = """
            namespace NexusLabs.Needlr
            {
                [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
                public sealed class DoNotAutoRegisterAttribute : System.Attribute { }

                [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
                public sealed class ScopedAttribute : System.Attribute { }

                [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
                public sealed class SingletonAttribute : System.Attribute { }

                [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
                public sealed class TransientAttribute : System.Attribute { }

                [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
                public sealed class InterceptAttribute : System.Attribute
                {
                    public InterceptAttribute(System.Type interceptorType) { InterceptorType = interceptorType; }
                    public System.Type InterceptorType { get; }
                    public int Order { get; set; } = 0;
                }

                [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
                public sealed class InterceptAttribute<TInterceptor> : System.Attribute
                    where TInterceptor : class, IMethodInterceptor
                {
                    public System.Type InterceptorType => typeof(TInterceptor);
                    public int Order { get; set; } = 0;
                }

                public interface IMethodInterceptor
                {
                    System.Threading.Tasks.ValueTask<object?> InterceptAsync(IMethodInvocation invocation);
                }

                public interface IMethodInvocation
                {
                    object Target { get; }
                    System.Reflection.MethodInfo Method { get; }
                    object?[] Arguments { get; }
                    System.Type[] GenericArguments { get; }
                    System.Threading.Tasks.ValueTask<object?> ProceedAsync();
                }

                public sealed class MethodInvocation : IMethodInvocation
                {
                    private readonly System.Func<System.Threading.Tasks.ValueTask<object?>> _proceed;
                    public MethodInvocation(object target, System.Reflection.MethodInfo method, object?[] arguments, System.Func<System.Threading.Tasks.ValueTask<object?>> proceed)
                    {
                        Target = target;
                        Method = method;
                        Arguments = arguments;
                        GenericArguments = System.Type.EmptyTypes;
                        _proceed = proceed;
                    }
                    public object Target { get; }
                    public System.Reflection.MethodInfo Method { get; }
                    public object?[] Arguments { get; }
                    public System.Type[] GenericArguments { get; }
                    public System.Threading.Tasks.ValueTask<object?> ProceedAsync() => _proceed();
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
