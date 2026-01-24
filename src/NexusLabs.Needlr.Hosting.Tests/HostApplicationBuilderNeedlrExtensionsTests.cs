using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;
using NexusLabs.Needlr.Injection.Scrutor;

using System.Reflection;

using Xunit;

namespace NexusLabs.Needlr.Hosting.Tests;

public sealed class HostApplicationBuilderNeedlrExtensionsTests
{
    [Fact]
    public void UseNeedlrDiscovery_WithCustomSyringe_UsesSyringe()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        var syringe = new Syringe()
            .UsingReflection()
            .UsingScrutorTypeRegistrar()
            .UsingAssemblyProvider(b => b
                .MatchingAssemblies(x => x.Contains("NexusLabs.Needlr"))
                .Build());
        
        // Act
        var result = builder.UseNeedlrDiscovery(syringe);

        // Assert
        Assert.Same(builder, result);
    }

    [Fact]
    public void UseNeedlrDiscovery_RegistersPluginFactory()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        var syringe = new Syringe()
            .UsingReflection()
            .UsingScrutorTypeRegistrar()
            .UsingAssemblyProvider(b => b
                .MatchingAssemblies(x => x.Contains("NexusLabs.Needlr"))
                .Build());
        
        // Act
        builder.UseNeedlrDiscovery(syringe);
        var host = builder.Build();

        // Assert
        var pluginFactory = host.Services.GetService<IPluginFactory>();
        Assert.NotNull(pluginFactory);
        host.Dispose();
    }

    [Fact]
    public void UseNeedlrDiscovery_RegistersCandidateAssemblies()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        var syringe = new Syringe()
            .UsingReflection()
            .UsingScrutorTypeRegistrar()
            .UsingAssemblyProvider(b => b
                .MatchingAssemblies(x => x.Contains("NexusLabs.Needlr"))
                .Build());
        
        // Act
        builder.UseNeedlrDiscovery(syringe);
        var host = builder.Build();

        // Assert
        var assemblies = host.Services.GetService<IReadOnlyList<Assembly>>();
        Assert.NotNull(assemblies);
        Assert.NotEmpty(assemblies);
        host.Dispose();
    }

    [Fact]
    public void UseNeedlrDiscovery_RegistersServiceProviderBuilder()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        var syringe = new Syringe()
            .UsingReflection()
            .UsingScrutorTypeRegistrar()
            .UsingAssemblyProvider(b => b
                .MatchingAssemblies(x => x.Contains("NexusLabs.Needlr"))
                .Build());
        
        // Act
        builder.UseNeedlrDiscovery(syringe);
        var host = builder.Build();

        // Assert
        var spb = host.Services.GetService<IServiceProviderBuilder>();
        Assert.NotNull(spb);
        host.Dispose();
    }

    [Fact]
    public void UseNeedlrDiscovery_WithNullBuilder_ThrowsArgumentNullException()
    {
        // Arrange
        HostApplicationBuilder builder = null!;
        var syringe = new Syringe().UsingReflection();
        
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => builder.UseNeedlrDiscovery(syringe));
    }

    [Fact]
    public void RunHostPlugins_WithNullHost_ThrowsArgumentNullException()
    {
        // Arrange
        IHost host = null!;
        
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => host.RunHostPlugins());
    }

    [Fact]
    public void RunHostPlugins_AfterUseNeedlrDiscovery_ReturnsHost()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        var syringe = new Syringe()
            .UsingReflection()
            .UsingScrutorTypeRegistrar()
            .UsingAssemblyProvider(b => b
                .MatchingAssemblies(x => x.Contains("NexusLabs.Needlr"))
                .Build());
        builder.UseNeedlrDiscovery(syringe);
        var host = builder.Build();
        
        // Act
        var result = host.RunHostPlugins();

        // Assert
        Assert.Same(host, result);
        host.Dispose();
    }

    [Fact]
    public void UseNeedlrDiscovery_WithLogger_LogsDiscovery()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        var loggerFactory = new TestLoggerFactory();
        var logger = loggerFactory.CreateLogger("Test");
        var syringe = new Syringe()
            .UsingReflection()
            .UsingScrutorTypeRegistrar()
            .UsingAssemblyProvider(b => b
                .MatchingAssemblies(x => x.Contains("NexusLabs.Needlr"))
                .Build());
        
        // Act
        builder.UseNeedlrDiscovery(syringe, logger);

        // Assert
        Assert.True(loggerFactory.LogCount > 0);
    }

    private sealed class TestLoggerFactory : ILoggerFactory
    {
        public int LogCount { get; private set; }

        public void AddProvider(ILoggerProvider provider) { }
        public void Dispose() { }

        public ILogger CreateLogger(string categoryName)
        {
            return new TestLogger(this);
        }

        public void IncrementLogCount() => LogCount++;

        private sealed class TestLogger(TestLoggerFactory factory) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                factory.IncrementLogCount();
            }
        }
    }
}
