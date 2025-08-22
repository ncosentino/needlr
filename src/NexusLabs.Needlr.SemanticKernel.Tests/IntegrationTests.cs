using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.SemanticKernel;

using System.ComponentModel;

namespace NexusLabs.Needlr.SemanticKernel.Tests;

public class IntegrationTests
{
    [Fact]
    public void CreateKernel_FullIntegrationWithSyringe_CreatesKernelWithPlugins()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TestSetting"] = "TestValue"
            })
            .Build();
        
        var kernel = new Syringe()
            .UsingSemanticKernel(syringe => syringe
                .Configure((opts) =>
                {
                    // Add a simple chat completion mock
                    opts.KernelBuilder.Services.AddSingleton<TestChatService>();
                })
                .AddSemanticKernelPlugin<IntegrationTestPlugin>()
                .AddSemanticKernelPluginsFromAssemblies())
            .BuildServiceProvider(configuration)
            .GetRequiredService<IKernelFactory>()
            .CreateKernel();
        
        Assert.NotNull(kernel);
        Assert.Contains(kernel.Plugins, p => p.Name == nameof(IntegrationTestPlugin));
        
        var plugins = kernel.Plugins;
        Assert.NotEmpty(plugins);
    }
    
    [Fact]
    public void CreateKernel_WithAssemblyScanning_DiscoversAndRegistersPlugins()
    {
        var config = new ConfigurationBuilder().Build();
        
        var kernel = new Syringe()
            .UsingSemanticKernel(syringe => syringe
                .AddSemanticKernelPluginsFromAssemblies())
            .UsingPostPluginRegistrationCallback(svc => svc.AddSingleton<TestDependency>())
            .BuildServiceProvider(config)
            .GetRequiredService<IKernelFactory>()
            .CreateKernel();
        
        var plugins = kernel.Plugins;
        Assert.Contains(plugins, p => p.Name == nameof(AutoDiscoveredPlugin));
    }
    
    [Fact]
    public void CreateKernel_WithBothInstanceAndStaticFlags_SupportsAllPluginTypes()
    {
        var config = new ConfigurationBuilder().Build();
        
        var kernel = new Syringe()
            .UsingSemanticKernel(syringe => syringe
                .AddSemanticKernelPluginsFromAssemblies(
                    includeInstancePlugins: true,
                    includeStaticPlugins: true))
            .BuildServiceProvider(config)
            .GetRequiredService<IKernelFactory>()
            .CreateKernel();
        
        var plugins = kernel.Plugins;
        Assert.Contains(plugins, p => p.Name.Contains("Static"));
    }
    
    [Fact]
    public void CreateKernel_WithMultipleKernelBuilderPlugins_AppliesAllPlugins()
    {
        var config = new ConfigurationBuilder().Build();
        
        var kernel = new Syringe()
            .UsingSemanticKernel()
            .BuildServiceProvider(config)
            .GetRequiredService<IKernelFactory>()
            .CreateKernel();
        
        Assert.NotNull(kernel.Services.GetService<TestKernelBuilderPlugin1.TestService1>());
        Assert.NotNull(kernel.Services.GetService<TestKernelBuilderPlugin2.TestService2>());
    }
    
    [Fact]
    public void CreateKernel_CalledMultipleTimes_CreatesIndependentKernelInstances()
    {
        var config = new ConfigurationBuilder().Build();
        
        var factory = new Syringe()
            .UsingSemanticKernel()
            .BuildServiceProvider(config)
            .GetRequiredService<IKernelFactory>();
        
        var kernel1 = factory.CreateKernel();
        var kernel2 = factory.CreateKernel();
        
        Assert.NotSame(kernel1, kernel2);
        Assert.NotSame(kernel1.Services, kernel2.Services);
    }
}

public class IntegrationTestPlugin
{
    [KernelFunction("IntegrationTest")]
    [Description("Integration test function")]
    public string TestFunction() => "Integration Test";
}

public class AutoDiscoveredPlugin
{
    private readonly TestDependency? _dependency;
    
    public AutoDiscoveredPlugin(TestDependency? dependency = null)
    {
        _dependency = dependency;
    }
    
    [KernelFunction("AutoDiscovered")]
    [Description("Auto-discovered function")]
    public string AutoDiscoveredFunction() => _dependency?.GetValue() ?? "No Dependency";
}

public static class StaticAutoDiscoveredPlugin
{
    [KernelFunction("StaticAutoDiscovered")]
    [Description("Static auto-discovered function")]
    public static string StaticFunction() => "Static";
}

public class TestDependency
{
    public string GetValue() => "Dependency Value";
}

public class TestChatService
{
    public string GetResponse(string prompt) => $"Response to: {prompt}";
}

public class TestKernelBuilderPlugin1 : IKernelBuilderPlugin
{    
    public void Configure(KernelBuilderPluginOptions options)
    {
        options.KernelBuilder.Services.AddSingleton<TestService1>();
    }
    
    public class TestService1 { }
}

public class TestKernelBuilderPlugin2 : IKernelBuilderPlugin
{
    
    public void Configure(KernelBuilderPluginOptions options)
    {
        options.KernelBuilder.Services.AddSingleton<TestService2>();
    }
    
    public class TestService2 { }
}