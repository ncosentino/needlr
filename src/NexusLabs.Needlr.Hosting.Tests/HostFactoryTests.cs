using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;
using NexusLabs.Needlr.Injection.Scrutor;

using Xunit;

namespace NexusLabs.Needlr.Hosting.Tests;

public sealed class HostFactoryTests
{
    [Fact]
    public void Create_WithDefaultOptions_ReturnsHost()
    {
        // Arrange
        var syringe = new Syringe()
            .UsingReflection()
            .UsingScrutorTypeRegistrar()
            .UsingAssemblyProvider(builder => builder
                .MatchingAssemblies(x => x.Contains("NexusLabs.Needlr"))
                .Build());

        var hostSyringe = syringe.ForHost();
        
        // Act
        var host = hostSyringe.BuildHost();

        // Assert
        Assert.NotNull(host);
        host.Dispose();
    }

    [Fact]
    public void Create_WithCustomOptions_AppliesOptions()
    {
        // Arrange
        var syringe = new Syringe()
            .UsingReflection()
            .UsingScrutorTypeRegistrar()
            .UsingAssemblyProvider(builder => builder
                .MatchingAssemblies(x => x.Contains("NexusLabs.Needlr"))
                .Build());

        var hostSyringe = syringe
            .ForHost()
            .UsingOptions(() => CreateHostOptions.Default
                .UsingApplicationName("TestApp")
                .UsingEnvironmentName("Testing"));
        
        // Act
        var host = hostSyringe.BuildHost();

        // Assert
        Assert.NotNull(host);
        var env = host.Services.GetRequiredService<IHostEnvironment>();
        Assert.Equal("TestApp", env.ApplicationName);
        Assert.Equal("Testing", env.EnvironmentName);
        host.Dispose();
    }

    [Fact]
    public void Create_WithConfigurationCallback_InvokesCallback()
    {
        // Arrange
        var callbackInvoked = false;
        var syringe = new Syringe()
            .UsingReflection()
            .UsingScrutorTypeRegistrar()
            .UsingAssemblyProvider(builder => builder
                .MatchingAssemblies(x => x.Contains("NexusLabs.Needlr"))
                .Build());

        var hostSyringe = syringe
            .ForHost()
            .UsingConfigurationCallback((builder, options) =>
            {
                callbackInvoked = true;
            });
        
        // Act
        var host = hostSyringe.BuildHost();

        // Assert
        Assert.True(callbackInvoked);
        host.Dispose();
    }

    [Fact]
    public void Create_WithPrePluginCallback_InvokesBeforePlugins()
    {
        // Arrange
        var preCallbackOrder = 0;
        var callbackOrder = 0;
        
        var syringe = new Syringe()
            .UsingReflection()
            .UsingScrutorTypeRegistrar()
            .UsingAssemblyProvider(builder => builder
                .MatchingAssemblies(x => x.Contains("NexusLabs.Needlr"))
                .Build());

        var options = CreateHostOptions.Default
            .UsingPrePluginRegistrationCallback(services =>
            {
                preCallbackOrder = ++callbackOrder;
            });

        var hostSyringe = syringe
            .ForHost()
            .UsingOptions(() => options);
        
        // Act
        var host = hostSyringe.BuildHost();

        // Assert
        Assert.Equal(1, preCallbackOrder);
        host.Dispose();
    }

    [Fact]
    public void Create_WithPostPluginCallback_InvokesAfterPlugins()
    {
        // Arrange
        var postCallbackInvoked = false;
        
        var syringe = new Syringe()
            .UsingReflection()
            .UsingScrutorTypeRegistrar()
            .UsingAssemblyProvider(builder => builder
                .MatchingAssemblies(x => x.Contains("NexusLabs.Needlr"))
                .Build());

        var options = CreateHostOptions.Default
            .UsingPostPluginRegistrationCallback(services =>
            {
                postCallbackInvoked = true;
            });

        var hostSyringe = syringe
            .ForHost()
            .UsingOptions(() => options);
        
        // Act
        var host = hostSyringe.BuildHost();

        // Assert
        Assert.True(postCallbackInvoked);
        host.Dispose();
    }

    [Fact]
    public void Create_WithLogger_LogsCreation()
    {
        // Arrange
        var loggerFactory = new TestLoggerFactory();
        var logger = loggerFactory.CreateLogger("Test");
        
        var syringe = new Syringe()
            .UsingReflection()
            .UsingScrutorTypeRegistrar()
            .UsingAssemblyProvider(builder => builder
                .MatchingAssemblies(x => x.Contains("NexusLabs.Needlr"))
                .Build());

        var options = new CreateHostOptions(new HostApplicationBuilderSettings(), logger);

        var hostSyringe = syringe
            .ForHost()
            .UsingOptions(() => options);
        
        // Act
        var host = hostSyringe.BuildHost();

        // Assert
        Assert.True(loggerFactory.LogCount > 0);
        host.Dispose();
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
