using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

using System.ComponentModel;

namespace NexusLabs.Needlr.SemanticKernel.Tests;

public sealed class PluginScannerIntegrationTests
{
    [Fact]
    public void AddSemanticKernelPluginsFromAssemblies_WithInstancePluginsOnly_DiscoversInstancePlugins()
    {
        var config = new ConfigurationBuilder().Build();
        
        var kernel = new Syringe()
            .UsingReflection()
            .UsingSemanticKernel(syringe => syringe
                .AddSemanticKernelPluginsFromAssemblies(
                    includeInstancePlugins: true,
                    includeStaticPlugins: false))
            .BuildServiceProvider(config)
            .GetRequiredService<IKernelFactory>()
            .CreateKernel();
        
        var plugins = kernel.Plugins;
        Assert.NotEmpty(plugins);
    }
    
    [Fact]
    public void AddSemanticKernelPluginsFromAssemblies_WithStaticPluginsOnly_DiscoversStaticPlugins()
    {
        var config = new ConfigurationBuilder().Build();
        
        var kernel = new Syringe()
            .UsingReflection()
            .UsingSemanticKernel(syringe => syringe
                .AddSemanticKernelPluginsFromAssemblies(
                    includeInstancePlugins: false,
                    includeStaticPlugins: true))
            .BuildServiceProvider(config)
            .GetRequiredService<IKernelFactory>()
            .CreateKernel();
        
        var plugins = kernel.Plugins;
        var staticPlugin = plugins.FirstOrDefault(p => p.Name.Contains("Static"));
        Assert.NotNull(staticPlugin);
    }
    
    [Fact]
    public void AddSemanticKernelPluginsFromProvider_WithRegisteredServices_DiscoversPluginsFromProvider()
    {
        var config = new ConfigurationBuilder().Build();
        
        var kernel = new Syringe()
            .UsingReflection()
            .UsingSemanticKernel(syringe => syringe
                .AddSemanticKernelPluginsFromProvider())
            .UsingPostPluginRegistrationCallback(services =>
            {
                services.AddSingleton<ServiceProviderTestPlugin>();
            })
            .BuildServiceProvider(config)
            .GetRequiredService<IKernelFactory>()
            .CreateKernel();
        
        var plugins = kernel.Plugins;
        Assert.NotEmpty(plugins);
    }
    
    [Fact]
    public void AddSemanticKernelPlugins_MultipleSources_CombinesAllPluginSources()
    {
        var config = new ConfigurationBuilder().Build();
        
        var kernel = new Syringe()
            .UsingReflection()
            .UsingSemanticKernel(syringe => syringe
                .AddSemanticKernelPluginsFromProvider()
                .AddSemanticKernelPluginsFromAssemblies()
                .AddSemanticKernelPlugin<DirectlyAddedPlugin>())
            .UsingPostPluginRegistrationCallback(services =>
            {
                services.AddSingleton<ServiceProviderTestPlugin>();
            })
            .BuildServiceProvider(config)
            .GetRequiredService<IKernelFactory>()
            .CreateKernel();
        
        var plugins = kernel.Plugins;
        Assert.NotEmpty(plugins);
        Assert.Contains(plugins, p => p.Name == nameof(DirectlyAddedPlugin));
    }
}

public class ServiceProviderTestPlugin
{
    [KernelFunction("ServiceProviderFunction")]
    [Description("A function from service provider")]
    public string TestMethod() => "from service provider";
}

public class DirectlyAddedPlugin
{
    [KernelFunction("DirectlyAdded")]
    [Description("A directly added function")]
    public string DirectMethod() => "directly added";
}

public static class StaticTestPluginForIntegration
{
    [KernelFunction("StaticIntegration")]
    [Description("Static integration test")]
    public static string StaticMethod() => "static integration";
}