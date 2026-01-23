using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using NexusLabs.Needlr.AspNet;

using System.Reflection;

using Xunit;

namespace NexusLabs.Needlr.SignalR.Tests;

#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access
#pragma warning disable IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling

public sealed class SignalRExtensionsTests : IDisposable
{
    private WebApplication? _app;
    private bool _disposed;

    [Fact]
    public void UseSignalRHubsWithReflection_WithNullApp_ThrowsArgumentNullException()
    {
        // Arrange
        WebApplication app = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => app.UseSignalRHubsWithReflection());
    }

    [Fact]
    public void UseSignalRHubsWithReflection_WithPluginFactory_ReturnsApp()
    {
        // Arrange
        var mockPluginFactory = new Mock<IPluginFactory>();
        mockPluginFactory.Setup(f => f.CreatePluginsFromAssemblies<IHubRegistrationPlugin>(It.IsAny<IEnumerable<Assembly>>()))
            .Returns([new TestHubRegistrationPlugin()]);
        
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSignalR();
        builder.Services.AddSingleton<IPluginFactory>(mockPluginFactory.Object);
        builder.Services.AddSingleton<IReadOnlyList<Assembly>>([typeof(TestHub).Assembly]);
        
        _app = builder.Build();

        // Act
        var result = _app.UseSignalRHubsWithReflection(mockPluginFactory.Object, [typeof(TestHub).Assembly]);

        // Assert
        Assert.Same(_app, result);
    }

    [Fact]
    public void UseSignalRHubsWithReflection_WithoutExplicitPluginFactory_UsesServiceProvider()
    {
        // Arrange
        var mockPluginFactory = new Mock<IPluginFactory>();
        mockPluginFactory.Setup(f => f.CreatePluginsFromAssemblies<IHubRegistrationPlugin>(It.IsAny<IEnumerable<Assembly>>()))
            .Returns([new TestHubRegistrationPlugin()]);
        
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSignalR();
        builder.Services.AddSingleton<IPluginFactory>(mockPluginFactory.Object);
        builder.Services.AddSingleton<IReadOnlyList<Assembly>>([typeof(TestHub).Assembly]);
        
        _app = builder.Build();

        // Act - no explicit pluginFactory parameter
        var result = _app.UseSignalRHubsWithReflection();

        // Assert
        Assert.Same(_app, result);
    }

    [Fact]
    public void UseSignalRHubsWithReflection_WithExplicitAssemblies_UsesProvidedAssemblies()
    {
        // Arrange
        var mockPluginFactory = new Mock<IPluginFactory>();
        mockPluginFactory.Setup(f => f.CreatePluginsFromAssemblies<IHubRegistrationPlugin>(It.IsAny<IEnumerable<Assembly>>()))
            .Returns([]);
        
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSignalR();
        builder.Services.AddSingleton<IPluginFactory>(mockPluginFactory.Object);
        
        _app = builder.Build();
        var assemblies = new[] { typeof(TestHub).Assembly };

        // Act
        _app.UseSignalRHubsWithReflection(mockPluginFactory.Object, assemblies);

        // Assert
        mockPluginFactory.Verify(f => f.CreatePluginsFromAssemblies<IHubRegistrationPlugin>(It.IsAny<IEnumerable<Assembly>>()), Times.Once);
    }

    [Fact]
    public void AddSignalRHubRegistrationWithReflection_WithNullServices_ThrowsArgumentNullException()
    {
        // Arrange
        IServiceCollection services = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => services.AddSignalRHubRegistrationWithReflection());
    }

    [Fact]
    public void AddSignalRHubRegistrationWithReflection_RegistersPlugin()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddSignalRHubRegistrationWithReflection();

        // Assert
        Assert.Same(services, result);
        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IWebApplicationPlugin));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void AddSignalRHubRegistrationWithReflection_RegistersSignalRHubRegistrationPlugin()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddSignalRHubRegistrationWithReflection();
        var provider = services.BuildServiceProvider();
        var plugins = provider.GetServices<IWebApplicationPlugin>();

        // Assert
        Assert.Contains(plugins, p => p is SignalRHubRegistrationPlugin);
    }

    public void Dispose()
    {
        if (!_disposed && _app != null)
        {
            _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _disposed = true;
        }
    }
}

#pragma warning restore IL2026
#pragma warning restore IL3050
