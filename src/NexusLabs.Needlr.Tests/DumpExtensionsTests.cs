using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace NexusLabs.Needlr.Tests;

/// <summary>
/// Tests for IServiceCollection.Dump() and IServiceProvider.Dump() debug output.
/// </summary>
public sealed class DumpExtensionsTests
{
    [Fact]
    public void Dump_EmptyServiceCollection_ReturnsEmptyIndicator()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.Dump();

        // Assert
        Assert.NotNull(result);
        Assert.Contains("0 registrations", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Dump_SingleRegistration_ShowsServiceInfo()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<ITestService, TestService>();

        // Act
        var result = services.Dump();

        // Assert
        Assert.Contains("ITestService", result);
        Assert.Contains("TestService", result);
        Assert.Contains("Transient", result);
    }

    [Fact]
    public void Dump_MultipleRegistrations_ShowsAllServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<ITestService, TestService>();
        services.AddScoped<IAnotherService, AnotherService>();
        services.AddSingleton<IThirdService, ThirdService>();

        // Act
        var result = services.Dump();

        // Assert
        Assert.Contains("ITestService", result);
        Assert.Contains("IAnotherService", result);
        Assert.Contains("IThirdService", result);
        Assert.Contains("3 registrations", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Dump_WithDumpOptions_CanFilterByLifetime()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<ITestService, TestService>();
        services.AddSingleton<IAnotherService, AnotherService>();
        var options = new DumpOptions { LifetimeFilter = ServiceLifetime.Singleton };

        // Act
        var result = services.Dump(options);

        // Assert
        Assert.DoesNotContain("ITestService", result);
        Assert.Contains("IAnotherService", result);
    }

    [Fact]
    public void Dump_WithDumpOptions_CanFilterByServiceType()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<ITestService, TestService>();
        services.AddTransient<IAnotherService, AnotherService>();
        var options = new DumpOptions { ServiceTypeFilter = t => t.Name.Contains("Another") };

        // Act
        var result = services.Dump(options);

        // Assert
        Assert.DoesNotContain("ITestService", result);
        Assert.Contains("IAnotherService", result);
    }

    [Fact]
    public void Dump_ServiceProvider_ShowsRegistrations()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<ITestService, TestService>();
        services.AddSingleton<IServiceCollection>(services);
        var provider = services.BuildServiceProvider();

        // Act
        var result = provider.Dump();

        // Assert
        Assert.Contains("ITestService", result);
    }

    [Fact]
    public void Dump_GroupsByLifetime_WhenOptionSet()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<ITestService, TestService>();
        services.AddScoped<IAnotherService, AnotherService>();
        services.AddSingleton<IThirdService, ThirdService>();
        var options = new DumpOptions { GroupByLifetime = true };

        // Act
        var result = services.Dump(options);

        // Assert - should have section headers
        Assert.Contains("Singleton", result);
        Assert.Contains("Scoped", result);
        Assert.Contains("Transient", result);
    }

    [Fact]
    public void Dump_NullServiceCollection_ThrowsArgumentNullException()
    {
        // Arrange
        IServiceCollection? services = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => services!.Dump());
    }

    [Fact]
    public void Dump_NullServiceProvider_ThrowsArgumentNullException()
    {
        // Arrange
        IServiceProvider? provider = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => provider!.Dump());
    }

    public interface ITestService { }
    public interface IAnotherService { }
    public interface IThirdService { }
    public class TestService : ITestService { }
    public class AnotherService : IAnotherService { }
    public class ThirdService : IThirdService { }
}
