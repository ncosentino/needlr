// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests;

/// <summary>
/// Tests for ServiceCatalog generation.
/// </summary>
public sealed class ServiceCatalogGeneratorTests
{
    [Fact]
    public void ServiceCatalog_GeneratesForSimpleService()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry]

            namespace TestApp
            {
                public interface ILogger { }

                [Singleton]
                public sealed class ConsoleLogger : ILogger { }
            }
            """;

        var catalogContent = GetServiceCatalogOutput(source);

        // Should generate a ServiceCatalog class
        Assert.Contains("public sealed class ServiceCatalog", catalogContent);
        Assert.Contains("IServiceCatalog", catalogContent);
        
        // Should include the registered service
        Assert.Contains("ConsoleLogger", catalogContent);
        Assert.Contains("ILogger", catalogContent);
        Assert.Contains("Singleton", catalogContent);
    }

    [Fact]
    public void ServiceCatalog_IncludesConstructorParameters()
    {
        var source = """
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry]

            namespace TestApp
            {
                public interface ICache { }
                public interface ISerializer { }
                public interface IDataService { }

                [Singleton]
                public sealed class MemoryCache : ICache { }

                [Singleton]
                public sealed class JsonSerializer : ISerializer { }

                [Scoped]
                public sealed class DataService : IDataService
                {
                    public DataService(ICache cache, ISerializer serializer) { }
                }
            }
            """;

        var catalogContent = GetServiceCatalogOutput(source);
        
        // DataService must be in catalog
        Assert.Contains("DataService", catalogContent);

        // Should include ConstructorParameterEntry records - check the type is used
        Assert.Contains("ConstructorParameterEntry", catalogContent);
        
        // Check for the interface type names in constructor params 
        Assert.Contains("ICache", catalogContent);
        Assert.Contains("ISerializer", catalogContent);
    }

    [Fact]
    public void ServiceCatalog_IncludesDecorators()
    {
        var source = """
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry]

            namespace TestApp
            {
                public interface IService { void Execute(); }

                public sealed class RealService : IService { public void Execute() { } }

                [DecoratorFor<IService>(Order = 1)]
                public sealed class LoggingDecorator : IService
                {
                    private readonly IService _inner;
                    public LoggingDecorator(IService inner) { _inner = inner; }
                    public void Execute() => _inner.Execute();
                }
            }
            """;

        var catalogContent = GetServiceCatalogOutput(source);

        // Should have Decorators collection
        Assert.Contains("Decorators", catalogContent);
        Assert.Contains("DecoratorCatalogEntry", catalogContent);
        Assert.Contains("LoggingDecorator", catalogContent);
        Assert.Contains("IService", catalogContent);
        // Positional record uses constructor syntax, not property syntax
        Assert.Contains(", 1,", catalogContent); // Order = 1 in positional constructor
    }

    [Fact]
    public void ServiceCatalog_IncludesHostedServices()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Extensions.Hosting;

            [assembly: GenerateTypeRegistry]

            namespace TestApp
            {
                public sealed class BackgroundWorker : BackgroundService
                {
                    protected override Task ExecuteAsync(CancellationToken stoppingToken) 
                        => Task.CompletedTask;
                }
            }
            """;

        // Define hosting types inline
        var hostingSource = """
            namespace Microsoft.Extensions.Hosting
            {
                public interface IHostedService
                {
                    System.Threading.Tasks.Task StartAsync(System.Threading.CancellationToken cancellationToken);
                    System.Threading.Tasks.Task StopAsync(System.Threading.CancellationToken cancellationToken);
                }

                public abstract class BackgroundService : IHostedService
                {
                    public virtual System.Threading.Tasks.Task StartAsync(System.Threading.CancellationToken cancellationToken) 
                        => System.Threading.Tasks.Task.CompletedTask;
                    public virtual System.Threading.Tasks.Task StopAsync(System.Threading.CancellationToken cancellationToken) 
                        => System.Threading.Tasks.Task.CompletedTask;
                    protected abstract System.Threading.Tasks.Task ExecuteAsync(System.Threading.CancellationToken stoppingToken);
                }
            }
            """;

        var catalogContent = GetServiceCatalogOutputWithExtra(source, hostingSource);

        // Should have HostedServices collection
        Assert.Contains("HostedServices", catalogContent);
        Assert.Contains("HostedServiceCatalogEntry", catalogContent);
        Assert.Contains("BackgroundWorker", catalogContent);
    }

    [Fact]
    public void ServiceCatalog_IncludesOptions()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry]

            namespace TestApp
            {
                [Options(SectionName = "Database", ValidateOnStart = true)]
                public sealed class DatabaseOptions
                {
                    public string ConnectionString { get; set; } = "";
                }
            }
            """;

        var catalogContent = GetServiceCatalogOutput(source);

        // Should have Options collection
        Assert.Contains("Options", catalogContent);
        Assert.Contains("OptionsCatalogEntry", catalogContent);
        Assert.Contains("DatabaseOptions", catalogContent);
        // Positional record: section name is a string param, ValidateOnStart is a bool param
        Assert.Contains("\"Database\"", catalogContent);
        Assert.Contains("true", catalogContent);
    }

    [Fact]
    public void ServiceCatalog_ImplementsIServiceCatalog()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry]

            namespace TestApp
            {
                public sealed class SimpleService { }
            }
            """;

        var catalogContent = GetServiceCatalogOutput(source);

        // Should implement IServiceCatalog interface
        Assert.Contains("IServiceCatalog", catalogContent);
        Assert.Contains("public sealed class ServiceCatalog", catalogContent);
    }

    [Fact]
    public void ServiceCatalog_IncludesAssemblyNameAndGeneratedAt()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry]

            namespace TestApp
            {
                public sealed class SimpleService { }
            }
            """;

        var catalogContent = GetServiceCatalogOutput(source);

        // Should have assembly name and generation timestamp
        Assert.Contains("AssemblyName => \"TestAssembly\"", catalogContent);
        Assert.Contains("GeneratedAt =>", catalogContent);
    }

    [Fact]
    public void ServiceCatalog_IncludesPlugins()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry]

            namespace TestApp
            {
                public interface IPlugin { }

                [Plugin]
                public sealed class MyPlugin : IPlugin { }
            }
            """;

        var catalogContent = GetServiceCatalogOutput(source);

        // Should have Plugins collection
        Assert.Contains("Plugins", catalogContent);
        Assert.Contains("PluginCatalogEntry", catalogContent);
        Assert.Contains("MyPlugin", catalogContent);
    }

    [Fact]
    public void ServiceCatalog_IncludesInterceptedServices()
    {
        var source = """
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry]

            namespace TestApp
            {
                public interface IInterceptor
                {
                    object Intercept(object target, string methodName, object[] args, System.Func<object> proceed);
                }

                public sealed class LoggingInterceptor : IInterceptor
                {
                    public object Intercept(object target, string methodName, object[] args, System.Func<object> proceed)
                        => proceed();
                }

                public interface IDataService { void Save(); }

                [Intercept(typeof(LoggingInterceptor))]
                public sealed class DataService : IDataService { public void Save() { } }
            }
            """;

        var catalogContent = GetServiceCatalogOutput(source);

        // Should have InterceptedServices collection
        Assert.Contains("InterceptedServices", catalogContent);
        Assert.Contains("InterceptedServiceCatalogEntry", catalogContent);
        Assert.Contains("DataService", catalogContent);
        Assert.Contains("LoggingInterceptor", catalogContent);
    }

    [Fact]
    public void ServiceCatalog_MultipleServicesAllIncluded()
    {
        var source = """
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry]

            namespace TestApp
            {
                public interface IServiceA { }
                public interface IServiceB { }
                public interface IServiceC { }

                [Singleton]
                public sealed class ServiceA : IServiceA { }

                [Scoped]
                public sealed class ServiceB : IServiceB { }

                [Transient]
                public sealed class ServiceC : IServiceC { }
            }
            """;

        var catalogContent = GetServiceCatalogOutput(source);

        // All services should be included
        Assert.Contains("ServiceA", catalogContent);
        Assert.Contains("ServiceB", catalogContent);
        Assert.Contains("ServiceC", catalogContent);
        
        // All lifetimes should be represented
        Assert.Contains("Singleton", catalogContent);
        Assert.Contains("Scoped", catalogContent);
        Assert.Contains("Transient", catalogContent);
    }

    [Fact]
    public void ServiceCatalog_IsRegisteredInApplyDecorators()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry]

            namespace TestApp
            {
                public sealed class SimpleService { }
            }
            """;

        var typeRegistryContent = GetTypeRegistryOutput(source);

        // ApplyDecorators should register the ServiceCatalog as IServiceCatalog
        Assert.Contains("AddSingleton<global::NexusLabs.Needlr.Catalog.IServiceCatalog", typeRegistryContent);
        Assert.Contains("ServiceCatalog>", typeRegistryContent);
    }

    private static string GetServiceCatalogOutput(string source)
    {
        return GeneratorTestRunner.ForTypeRegistry()
            .WithReference<DecoratorForAttribute<object>>()
            .WithSource(source)
            .GetServiceCatalogOutput();
    }

    private static string GetTypeRegistryOutput(string source)
    {
        return GeneratorTestRunner.ForTypeRegistry()
            .WithReference<DecoratorForAttribute<object>>()
            .WithSource(source)
            .GetTypeRegistryOutput();
    }

    private static string GetServiceCatalogOutputWithExtra(string source, string extraSource)
    {
        return GeneratorTestRunner.ForTypeRegistry()
            .WithReference<DecoratorForAttribute<object>>()
            .WithSource(source)
            .WithSource(extraSource)
            .GetServiceCatalogOutput();
    }
}
