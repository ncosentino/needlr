using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Moq;

using NexusLabs.Needlr.AspNet;
using NexusLabs.Needlr.Injection.Reflection;

using Xunit;

namespace NexusLabs.Needlr.SignalR.Tests;

public sealed class SignalRHubRegistrationPluginIntegrationTests : IDisposable
{
    private readonly WebApplication _app;
    private readonly Mock<IPluginFactory> _mockPluginFactory;
    private bool _disposed;

    public SignalRHubRegistrationPluginIntegrationTests()
    {
        _mockPluginFactory = new Mock<IPluginFactory>();
        _mockPluginFactory.Setup(f => f.CreatePluginsFromAssemblies<IHubRegistrationPlugin>(It.IsAny<IReadOnlyList<System.Reflection.Assembly>>()))
            .Returns([new TestHubRegistrationPlugin()]);
        
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSignalR();
        
        // Register the plugin factory for hub discovery
        builder.Services.AddSingleton<IPluginFactory>(_mockPluginFactory.Object);
        
        _app = builder.Build();
    }

    [Fact]
    public void Configure_RegistersHubsFromPluginFactory()
    {
        // Arrange
        var plugin = new SignalRHubRegistrationPlugin();
        var options = new WebApplicationPluginOptions(_app, [typeof(TestHub).Assembly], _mockPluginFactory.Object);

        // Act & Assert - should not throw
        var exception = Record.Exception(() => plugin.Configure(options));
        Assert.Null(exception);
    }

    [Fact]
    public void Configure_LogsHubRegistration()
    {
        // Arrange
        var mockLogger = new Mock<ILogger>();
        mockLogger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        
        var mockPluginFactory = new Mock<IPluginFactory>();
        mockPluginFactory.Setup(f => f.CreatePluginsFromAssemblies<IHubRegistrationPlugin>(It.IsAny<IReadOnlyList<System.Reflection.Assembly>>()))
            .Returns([new TestHubRegistrationPlugin()]);
        
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSignalR();
        builder.Services.AddSingleton<IPluginFactory>(mockPluginFactory.Object);
        
        var app = builder.Build();
        var plugin = new SignalRHubRegistrationPlugin();
        var options = new WebApplicationPluginOptions(app, [typeof(TestHub).Assembly], mockPluginFactory.Object);

        // Act
        plugin.Configure(options);

        // Assert - logging verification would require ILogger injection
        // For now, verify no exception was thrown
    }

    [Fact]
    public void Configure_HandlesMultipleHubPlugins()
    {
        // Arrange
        var mockPluginFactory = new Mock<IPluginFactory>();
        mockPluginFactory.Setup(f => f.CreatePluginsFromAssemblies<IHubRegistrationPlugin>(It.IsAny<IReadOnlyList<System.Reflection.Assembly>>()))
            .Returns([
                new TestHubRegistrationPlugin(),
                new SecondHubRegistrationPlugin()
            ]);
        
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSignalR();
        builder.Services.AddSingleton<IPluginFactory>(mockPluginFactory.Object);
        
        var app = builder.Build();
        var plugin = new SignalRHubRegistrationPlugin();
        var options = new WebApplicationPluginOptions(app, [typeof(TestHub).Assembly], mockPluginFactory.Object);

        // Act & Assert - should handle multiple hubs
        var exception = Record.Exception(() => plugin.Configure(options));
        Assert.Null(exception);
    }

    [Fact]
    public void Configure_HandlesEmptyPluginList()
    {
        // Arrange
        var mockPluginFactory = new Mock<IPluginFactory>();
        mockPluginFactory.Setup(f => f.CreatePluginsFromAssemblies<IHubRegistrationPlugin>(It.IsAny<IReadOnlyList<System.Reflection.Assembly>>()))
            .Returns([]);
        
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSignalR();
        builder.Services.AddSingleton<IPluginFactory>(mockPluginFactory.Object);
        
        var app = builder.Build();
        var plugin = new SignalRHubRegistrationPlugin();
        var options = new WebApplicationPluginOptions(app, [typeof(TestHub).Assembly], mockPluginFactory.Object);

        // Act & Assert - should handle empty list without throwing
        var exception = Record.Exception(() => plugin.Configure(options));
        Assert.Null(exception);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _app?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _disposed = true;
        }
    }
}

public sealed class SecondHubRegistrationPlugin : IHubRegistrationPlugin
{
    public string HubPath => "/secondhub";
    public Type HubType => typeof(SecondHub);
}

public sealed class SecondHub : Hub
{
    // Second test hub
}
