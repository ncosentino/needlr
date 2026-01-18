using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

namespace NexusLabs.Needlr.SemanticKernel.Tests;

public sealed class KernelFactoryTests
{
    [Fact]
    public void CreateKernel_WithSyringeIntegration_ReturnsKernelInstance()
    {
        var config = new ConfigurationBuilder().Build();
        var factory = new Syringe()
            .UsingReflection()
            .UsingSemanticKernel()
            .BuildServiceProvider(config)
            .GetRequiredService<IKernelFactory>();
        
        var kernel = factory.CreateKernel();
        
        Assert.NotNull(kernel);
        Assert.IsType<Kernel>(kernel);
    }
    
    [Fact]
    public void CreateKernel_WithConfigurationAction_CallsConfigurationAction()
    {
        var config = new ConfigurationBuilder().Build();
        var configurationActionCalled = false;
        
        var factory = new Syringe()
            .UsingReflection()
            .UsingSemanticKernel(syringe =>
            {
                configurationActionCalled = true;
                return syringe;
            })
            .BuildServiceProvider(config)
            .GetRequiredService<IKernelFactory>();
        
        var kernel = factory.CreateKernel();
        
        Assert.True(configurationActionCalled);
    }
    
    [Fact]
    public void CreateKernel_WithMethodConfiguration_CallsMethodConfiguration()
    {
        var config = new ConfigurationBuilder().Build();
        var methodConfigCalled = false;
        
        var factory = new Syringe()
            .UsingReflection()
            .UsingSemanticKernel()
            .BuildServiceProvider(config)
            .GetRequiredService<IKernelFactory>();
        
        var kernel = factory.CreateKernel(opts =>
        {
            methodConfigCalled = true;
        });
        
        Assert.True(methodConfigCalled);
    }
    
    [Fact]
    public void CreateKernel_WithServiceRegistration_ProvidesAccessToServices()
    {
        var config = new ConfigurationBuilder().Build();
        
        var factory = new Syringe()
            .UsingReflection()
            .UsingSemanticKernel(syringe => syringe.Configure(opts =>
            {
                opts.KernelBuilder.Services.AddSingleton<TestService>();
            }))
            .BuildServiceProvider(config)
            .GetRequiredService<IKernelFactory>();
        
        var kernel = factory.CreateKernel();
        
        var testService = kernel.Services.GetService<TestService>();
        Assert.NotNull(testService);
    }
    
    [Fact]
    public void CreateKernel_WithKernelBuilderPlugins_AppliesAllPlugins()
    {
        TestKernelBuilderPlugin.WasConfigured = false;
        var config = new ConfigurationBuilder().Build();
        
        var factory = new Syringe()
            .UsingReflection()
            .UsingSemanticKernel()
            .BuildServiceProvider(config)
            .GetRequiredService<IKernelFactory>();
        
        var kernel = factory.CreateKernel();
        
        Assert.True(TestKernelBuilderPlugin.WasConfigured);
    }
    
    [Fact]
    public void CreateKernel_CalledMultipleTimes_CreatesIndependentInstances()
    {
        var config = new ConfigurationBuilder().Build();
        
        var factory = new Syringe()
            .UsingReflection()
            .UsingSemanticKernel()
            .BuildServiceProvider(config)
            .GetRequiredService<IKernelFactory>();
        
        var kernel1 = factory.CreateKernel();
        var kernel2 = factory.CreateKernel();
        
        Assert.NotSame(kernel1, kernel2);
        Assert.NotSame(kernel1.Services, kernel2.Services);
    }
    
    private class TestService { }
    private class ScopedTestService { }
}

public class TestKernelBuilderPlugin : IKernelBuilderPlugin
{
    public static bool WasConfigured { get; set; }
    
    public void Configure(KernelBuilderPluginOptions options)
    {
        WasConfigured = true;
        options.KernelBuilder.Services.AddSingleton<TestPluginService>();
    }
    
    public class TestPluginService { }
}