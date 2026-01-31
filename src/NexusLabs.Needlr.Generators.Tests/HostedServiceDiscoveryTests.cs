using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests;

/// <summary>
/// Tests that verify the source generator correctly discovers and registers
/// BackgroundService and IHostedService implementations.
/// </summary>
public sealed class HostedServiceDiscoveryTests
{
    [Fact]
    public void HostedService_BackgroundService_GeneratesRegistration()
    {
        // Arrange
        var source = """
            using Microsoft.Extensions.Hosting;
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TestNamespace" })]

            namespace TestNamespace
            {
                public class MyWorker : BackgroundService
                {
                    protected override Task ExecuteAsync(CancellationToken stoppingToken)
                    {
                        return Task.CompletedTask;
                    }
                }
            }
            """;

        // Act
        var generatedCode = RunGenerator(source);

        // Assert
        Assert.Contains("RegisterHostedServices", generatedCode);
        Assert.Contains("services.AddSingleton<global::TestNamespace.MyWorker>()", generatedCode);
        Assert.Contains("services.AddSingleton<global::Microsoft.Extensions.Hosting.IHostedService>(sp => sp.GetRequiredService<global::TestNamespace.MyWorker>())", generatedCode);
    }

    [Fact]
    public void HostedService_DirectIHostedServiceImplementation_GeneratesRegistration()
    {
        // Arrange
        var source = """
            using Microsoft.Extensions.Hosting;
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TestNamespace" })]

            namespace TestNamespace
            {
                public class MyHostedService : IHostedService
                {
                    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
                    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
                }
            }
            """;

        // Act
        var generatedCode = RunGenerator(source);

        // Assert
        Assert.Contains("RegisterHostedServices", generatedCode);
        Assert.Contains("services.AddSingleton<global::TestNamespace.MyHostedService>()", generatedCode);
        Assert.Contains("services.AddSingleton<global::Microsoft.Extensions.Hosting.IHostedService>(sp => sp.GetRequiredService<global::TestNamespace.MyHostedService>())", generatedCode);
    }

    [Fact]
    public void HostedService_WithDoNotAutoRegister_SkipsRegistration()
    {
        // Arrange
        var source = """
            using Microsoft.Extensions.Hosting;
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TestNamespace" })]

            namespace TestNamespace
            {
                [DoNotAutoRegister]
                public class MyWorker : BackgroundService
                {
                    protected override Task ExecuteAsync(CancellationToken stoppingToken)
                    {
                        return Task.CompletedTask;
                    }
                }
            }
            """;

        // Act
        var generatedCode = RunGenerator(source);

        // Assert - Should NOT contain registration for MyWorker
        Assert.DoesNotContain("MyWorker", generatedCode);
    }

    [Fact]
    public void HostedService_AbstractClass_SkipsRegistration()
    {
        // Arrange
        var source = """
            using Microsoft.Extensions.Hosting;
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TestNamespace" })]

            namespace TestNamespace
            {
                public abstract class BaseWorker : BackgroundService
                {
                }
            }
            """;

        // Act
        var generatedCode = RunGenerator(source);

        // Assert - Should NOT contain registration for abstract class
        Assert.DoesNotContain("BaseWorker", generatedCode);
    }

    [Fact]
    public void HostedService_MultipleWorkers_GeneratesAllRegistrations()
    {
        // Arrange
        var source = """
            using Microsoft.Extensions.Hosting;
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TestNamespace" })]

            namespace TestNamespace
            {
                public class WorkerA : BackgroundService
                {
                    protected override Task ExecuteAsync(CancellationToken stoppingToken)
                    {
                        return Task.CompletedTask;
                    }
                }

                public class WorkerB : BackgroundService
                {
                    protected override Task ExecuteAsync(CancellationToken stoppingToken)
                    {
                        return Task.CompletedTask;
                    }
                }
            }
            """;

        // Act
        var generatedCode = RunGenerator(source);

        // Assert - Both workers should be registered
        Assert.Contains("services.AddSingleton<global::TestNamespace.WorkerA>()", generatedCode);
        Assert.Contains("services.AddSingleton<global::TestNamespace.WorkerB>()", generatedCode);
    }

    [Fact]
    public void HostedService_CalledFromApplyDecorators()
    {
        // Arrange
        var source = """
            using Microsoft.Extensions.Hosting;
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TestNamespace" })]

            namespace TestNamespace
            {
                public class MyWorker : BackgroundService
                {
                    protected override Task ExecuteAsync(CancellationToken stoppingToken)
                    {
                        return Task.CompletedTask;
                    }
                }
            }
            """;

        // Act
        var generatedCode = RunGenerator(source);

        // Assert - ApplyDecorators should call RegisterHostedServices
        Assert.Contains("public static void ApplyDecorators(IServiceCollection services)", generatedCode);
        Assert.Contains("RegisterHostedServices(services);", generatedCode);
    }

    private static string RunGenerator(string source)
    {
        return GeneratorTestRunner.ForHostedServiceWithInlineTypes()
            .WithSource(source)
            .RunTypeRegistryGenerator();
    }
}
