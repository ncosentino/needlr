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
        var generatedCode = GeneratorTestRunner.ForInterceptorWithInlineTypes()
            .WithSource(source)
            .RunTypeRegistryGenerator();

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
        var generatedCode = GeneratorTestRunner.ForInterceptorWithInlineTypes()
            .WithSource(source)
            .RunTypeRegistryGenerator();

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
        var generatedCode = GeneratorTestRunner.ForInterceptorWithInlineTypes()
            .WithSource(source)
            .RunTypeRegistryGenerator();

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
        var generatedCode = GeneratorTestRunner.ForInterceptorWithInlineTypes()
            .WithSource(source)
            .RunTypeRegistryGenerator();

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
        var generatedCode = GeneratorTestRunner.ForInterceptorWithInlineTypes()
            .WithSource(source)
            .RunTypeRegistryGenerator();

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
        var generatedCode = GeneratorTestRunner.ForInterceptorWithInlineTypes()
            .WithSource(source)
            .RunTypeRegistryGenerator();

        // Assert
        Assert.Contains("ProcessOrder", generatedCode);
        Assert.Contains("void", generatedCode);
    }
}
