using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Extensions.Configuration;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Scrutor;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests;

public sealed class ScrutorAutomaticRegistrationTests
{
    private readonly IServiceProvider _serviceProvider;

    public ScrutorAutomaticRegistrationTests()
    {
        _serviceProvider = new Syringe()
            .UsingScrutorTypeRegistrar()
            .BuildServiceProvider();
    }

    [Fact]
    public void GetService_IMyAutomaticService_NotNull()
    {
        Assert.NotNull(_serviceProvider.GetService<IMyAutomaticService>());
    }

    [Fact]
    public void GetService_LazyIMyAutomaticService_NotNull()
    {
        var lazy = _serviceProvider.GetService<Lazy<IMyAutomaticService>>();
        Assert.NotNull(lazy);
        Assert.NotNull(lazy.Value);
    }

    [Fact]
    public void GetService_IMyAutomaticService_IsSingleInstance()
    {
        var instance1 = _serviceProvider.GetService<IMyAutomaticService>();
        var instance2 = _serviceProvider.GetService<IMyAutomaticService>();
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void GetService_IMyAutomaticService2_IsSingleInstance()
    {
        var instance1 = _serviceProvider.GetService<IMyAutomaticService2>();
        var instance2 = _serviceProvider.GetService<IMyAutomaticService2>();
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void GetService_LazyIMyAutomaticService2_NotNull()
    {
        var lazy = _serviceProvider.GetService<Lazy<IMyAutomaticService2>>();
        Assert.NotNull(lazy);
        Assert.NotNull(lazy.Value);
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
    public void GetService_LazyMyAutomaticService_NotNull()
    {
        var lazy = _serviceProvider.GetService<Lazy<MyAutomaticService>>();
        Assert.NotNull(lazy);
        Assert.NotNull(lazy.Value);
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

    [Fact]
    public void GetService_IInterfaceWithMultipleImplementations_ResolvesAsEnumerable()
    {
        var instances = _serviceProvider.GetService<IEnumerable<IInterfaceWithMultipleImplementations>>();
        Assert.NotNull(instances);
        Assert.NotEmpty(instances);
    }

    [Fact]
    public void GetService_IInterfaceWithMultipleImplementations_ResolvesAsIReadOnlyList()
    {
        var instances = _serviceProvider.GetService<IReadOnlyList<IInterfaceWithMultipleImplementations>>();
        Assert.NotNull(instances);
        Assert.NotEmpty(instances);
    }

    [Fact]
    public void GetService_IInterfaceWithMultipleImplementations_ResolvesAsIReadOnlyCollection()
    {
        var instances = _serviceProvider.GetService<IReadOnlyCollection<IInterfaceWithMultipleImplementations>>();
        Assert.NotNull(instances);
        Assert.NotEmpty(instances);
    }
}
