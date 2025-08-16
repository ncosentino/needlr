using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Extensions.Configuration;
using NexusLabs.Needlr.Injection;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests;

public sealed class RegistrationCallbacksWithSingletonDecoratorTests
{
    private readonly IServiceProvider _serviceProvider;

    public RegistrationCallbacksWithSingletonDecoratorTests()
    {
        _serviceProvider = new Syringe()
            .AddPostPluginRegistrationCallback(services =>
            {
                services.AddSingleton<MyManualService>();
                services.AddSingleton<IMyManualService, MyManualDecorator>(s =>
                    new MyManualDecorator(s.GetRequiredService<MyManualService>()));
            })
            .BuildServiceProvider();
    }

    [Fact]
    public void GetService_IMyManualService_NotNull()
    {
        Assert.NotNull(_serviceProvider.GetService<IMyManualService>());
    }

    [Fact]
    public void GetService_IMyManualService_IsSingleInstance()
    {
        var instance1 = _serviceProvider.GetService<IMyManualService>();
        var instance2 = _serviceProvider.GetService<IMyManualService>();
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void GetService_IMyManualService2_IsSingleInstance()
    {
        var instance1 = _serviceProvider.GetService<IMyManualService2>();
        var instance2 = _serviceProvider.GetService<IMyManualService2>();
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void GetService_IMyManualServiceAndMyManualService_AreNotSameInstance()
    {
        var theInterface = _serviceProvider.GetService<IMyManualService>();
        var theImplementation = _serviceProvider.GetService<MyManualService>();
        Assert.NotSame(theInterface, theImplementation);
    }

    [Fact]
    public void GetService_MyManualService_NotNull()
    {
        Assert.NotNull(_serviceProvider.GetService<MyManualService>());
    }

    [Fact]
    public void GetService_MyManualService_IsSingleInstance()
    {
        var instance1 = _serviceProvider.GetService<MyManualService>();
        var instance2 = _serviceProvider.GetService<MyManualService>();
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void GetService_MyDecorator_Null()
    {
        Assert.Null(_serviceProvider.GetService<MyManualDecorator>());
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
