using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Extensions.Configuration;
using NexusLabs.Needlr.Injection;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests;

public sealed class DefaultAutomaticRegistrationTests
{
    private readonly IServiceProvider _serviceProvider;

    public DefaultAutomaticRegistrationTests()
    {
        _serviceProvider = new Syringe().BuildServiceProvider();
    }

    [Fact]
    public void GetService_IMyAutomaticService_NotNull()
    {
        Assert.NotNull(_serviceProvider.GetService<IMyAutomaticService>());
    }

    [Fact]
    public void GetService_IMyAutomaticService_IsSingleInstance()
    {
        var instance1 = _serviceProvider.GetService<IMyAutomaticService>();
        var instance2 = _serviceProvider.GetService<IMyAutomaticService>();
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void GetService_IMyAutomaticService1And2_AreSame()
    {
        var instance1 = _serviceProvider.GetService<IMyAutomaticService>();
        var instance2 = _serviceProvider.GetService<IMyAutomaticService2>();
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void GetService_MyAutomaticService_Null()
    {
        Assert.NotNull(_serviceProvider.GetService<MyAutomaticService>());
    }

    [Fact]
    public void GetService_MyAutomaticService_IsSingleInstance()
    {
        var instance1 = _serviceProvider.GetService<MyAutomaticService>();
        var instance2 = _serviceProvider.GetService<MyAutomaticService>();
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void GetService_MyAutomaticServiceAndInterface_AreSame()
    {
        var instance1 = _serviceProvider.GetService<MyAutomaticService>();
        var instance2 = _serviceProvider.GetService<IMyAutomaticService>();
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void GetService_IConfiguration_NotNull()
    {
        Assert.NotNull(_serviceProvider.GetService<IConfiguration>());
    }

    [Fact]
    public void GetService_IConfiguration_IsSingleInstance()
    {
        var instance1 = _serviceProvider.GetService<IConfiguration>();
        var instance2 = _serviceProvider.GetService<IConfiguration>();
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void GetService_IServiceProvider_NotNull()
    {
        Assert.NotNull(_serviceProvider.GetService<IServiceProvider>());
    }

    [Fact]
    public void GetService_IServiceProvider_IsSingleInstance()
    {
        var instance1 = _serviceProvider.GetService<IServiceProvider>();
        var instance2 = _serviceProvider.GetService<IServiceProvider>();
        Assert.Same(instance1, instance2);
    }
}
