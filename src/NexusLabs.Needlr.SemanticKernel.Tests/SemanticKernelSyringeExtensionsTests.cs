using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

namespace NexusLabs.Needlr.SemanticKernel.Tests;

public sealed class SemanticKernelSyringeExtensionsTests
{
    [Fact]
    public void UsingKernelFactory_WithDefaultConfiguration_RegistersIKernelFactory()
    {
        var syringe = new Syringe()
            .UsingReflection();
        
        var config = new ConfigurationBuilder().Build();
        var result = syringe.UsingSemanticKernel();
        var serviceProvider = result.BuildServiceProvider(config);
        
        var kernelFactory = serviceProvider.GetService<IKernelFactory>();
        Assert.NotNull(kernelFactory);
    }
    
    [Fact]
    public void UsingKernelFactory_WithConfigurationCallback_AppliesConfiguration()
    {
        var syringe = new Syringe()
            .UsingReflection();
        var configurationCalled = false;
        
        var result = syringe.UsingSemanticKernel(syringe => syringe.Configure(opts =>
        {
            configurationCalled = true;
            Assert.NotNull(opts.ServiceProvider);
            Assert.NotNull(opts.KernelBuilder);
        }));
        
        var config = new ConfigurationBuilder().Build();
        var serviceProvider = result.BuildServiceProvider(config);
        var kernelFactory = serviceProvider.GetRequiredService<IKernelFactory>();
        
        var kernel = kernelFactory.CreateKernel();
        
        Assert.True(configurationCalled);
    }
    
    [Fact]
    public void UsingKernelFactory_CalledOnSyringe_ReturnsNewSyringeInstance()
    {
        var syringe = new Syringe()
            .UsingReflection();        
        var result = syringe.UsingSemanticKernel();        
        Assert.NotSame(syringe, result);
    }
    
    [Fact]
    public void UsingKernelFactory_RegisteredInServiceProvider_RegistersAsSingleton()
    {
        var syringe = new Syringe()
            .UsingReflection();
        
        var config = new ConfigurationBuilder().Build();
        var serviceProvider = syringe.UsingSemanticKernel().BuildServiceProvider(config);
        
        var factory1 = serviceProvider.GetRequiredService<IKernelFactory>();
        var factory2 = serviceProvider.GetRequiredService<IKernelFactory>();
        
        Assert.Same(factory1, factory2);
    }
    
    [Fact]
    public void UsingKernelFactory_WithPostPluginRegistrationCallback_WorksCorrectly()
    {
        var config = new ConfigurationBuilder().Build();
        var callbackExecuted = false;

        var serviceProvider = new Syringe()
            .UsingReflection()
            .UsingSemanticKernel()
            .UsingPostPluginRegistrationCallback(services =>
            {
                callbackExecuted = true;
                var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IKernelFactory));
                Assert.NotNull(descriptor);
            })
            .BuildServiceProvider(config);
        
        Assert.True(callbackExecuted);
        var kernelFactory = serviceProvider.GetService<IKernelFactory>();
        Assert.NotNull(kernelFactory);
    }
}