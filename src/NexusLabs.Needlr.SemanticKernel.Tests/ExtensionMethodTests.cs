using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.SemanticKernel;

using NexusLabs.Needlr.Injection;

using System.ComponentModel;

namespace NexusLabs.Needlr.SemanticKernel.Tests;

public sealed class ExtensionMethodTests
{
    [Fact]
    public void AddSemanticKernelPlugin_Should_Add_Plugin_To_Kernel()
    {
        var config = new ConfigurationBuilder().Build();
        
        var kernel = new Syringe()
            .UsingSemanticKernel(syringe => syringe
                .AddSemanticKernelPlugin<ExtensionTestPlugin>())
            .BuildServiceProvider(config)
            .GetRequiredService<IKernelFactory>()
            .CreateKernel();
        
        var plugin = kernel.Plugins.FirstOrDefault(p => p.Name == nameof(ExtensionTestPlugin));
        Assert.NotNull(plugin);
        Assert.NotEmpty(plugin);
    }
    
    [Fact]
    public void AddSemanticKernelPluginsFromAssemblies_Should_Work_With_Defaults()
    {
        var config = new ConfigurationBuilder().Build();
        
        var kernel = new Syringe()
            .UsingSemanticKernel(syringe => syringe
                .AddSemanticKernelPluginsFromAssemblies())
            .BuildServiceProvider(config)
            .GetRequiredService<IKernelFactory>()
            .CreateKernel();
        
        var plugins = kernel.Plugins;
        Assert.NotEmpty(plugins);
    }
    
    [Fact]
    public void AddSemanticKernelPluginsFromProvider_Should_Add_Registered_Plugins()
    {
        var config = new ConfigurationBuilder().Build();
        
        var kernel = new Syringe()
            .UsingSemanticKernel(syringe => syringe
                .AddSemanticKernelPluginsFromProvider())
            .UsingPostPluginRegistrationCallback(services =>
            {
                services.AddSingleton<RegisteredTestPlugin>();
            })
            .BuildServiceProvider(config)
            .GetRequiredService<IKernelFactory>()
            .CreateKernel();
        
        var plugins = kernel.Plugins;
        Assert.NotEmpty(plugins);
    }
    
    [Fact]
    public void Extension_Methods_Should_Be_Chainable()
    {
        var config = new ConfigurationBuilder().Build();
        
        var kernel = new Syringe()
            .UsingSemanticKernel(syringe => syringe
                .AddSemanticKernelPluginsFromProvider()
                .AddSemanticKernelPluginsFromAssemblies()
                .AddSemanticKernelPlugin<ExtensionTestPlugin>())
            .UsingPostPluginRegistrationCallback(services =>
            {
                services.TryAddSingleton<RegisteredTestPlugin>();
            })
            .BuildServiceProvider(config)
            .GetRequiredService<IKernelFactory>()
            .CreateKernel();
        
        var plugins = kernel.Plugins;
        Assert.NotEmpty(plugins);
        Assert.Contains(plugins, p => p.Name == nameof(ExtensionTestPlugin));
    }
}

public class ExtensionTestPlugin
{
    [KernelFunction("ExtensionTest")]
    [Description("Extension test function")]
    public string TestMethod() => "extension test";
}

public class RegisteredTestPlugin
{
    [KernelFunction("RegisteredTest")]
    [Description("Registered test function")]
    public string RegisteredMethod() => "registered test";
}