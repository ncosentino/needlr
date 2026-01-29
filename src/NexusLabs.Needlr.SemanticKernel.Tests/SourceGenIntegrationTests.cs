using System.ComponentModel;
using System.Reflection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;

using NexusLabs.Needlr.Generators;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;
using NexusLabs.Needlr.Injection.SourceGen;
using NexusLabs.Needlr.Injection.Reflection.PluginFactories;
using NexusLabs.Needlr.Injection.SourceGen.PluginFactories;

using Xunit;

namespace NexusLabs.Needlr.SemanticKernel.Tests;

/// <summary>
/// Integration tests verifying SemanticKernel plugin discovery works with both
/// reflection and source generation paths.
/// </summary>
public sealed class SourceGenIntegrationTests
{
    [Fact]
    public void SemanticKernel_PackageHasOwnTypeRegistry()
    {
        var semanticKernelAssembly = typeof(SemanticKernelSyringeExtensions).Assembly;
        var typeRegistryType = semanticKernelAssembly.GetType("NexusLabs.Needlr.SemanticKernel.Generated.TypeRegistry");

        Assert.NotNull(typeRegistryType);
        var getPluginTypesMethod = typeRegistryType.GetMethod("GetPluginTypes");
        Assert.NotNull(getPluginTypesMethod);
    }

    [Fact]
    public void SemanticKernel_PackageHasModuleInitializer()
    {
        var semanticKernelAssembly = typeof(SemanticKernelSyringeExtensions).Assembly;
        var moduleInitializerType = semanticKernelAssembly.GetType("NexusLabs.Needlr.SemanticKernel.Generated.NeedlrSourceGenModuleInitializer");

        Assert.NotNull(moduleInitializerType);
    }

    [Fact]
    public void SemanticKernel_PluginsRegisteredViaOwnTypeRegistry()
    {
        var semanticKernelAssembly = typeof(SemanticKernelSyringeExtensions).Assembly;
        var typeRegistryType = semanticKernelAssembly.GetType("NexusLabs.Needlr.SemanticKernel.Generated.TypeRegistry");
        Assert.NotNull(typeRegistryType);

        var getPluginTypesMethod = typeRegistryType.GetMethod("GetPluginTypes");
        Assert.NotNull(getPluginTypesMethod);

        var pluginTypes = (IReadOnlyList<PluginTypeInfo>)getPluginTypesMethod.Invoke(null, null)!;
        
        // SemanticKernel package may have IKernelBuilderPlugin implementations
        Assert.NotNull(pluginTypes);
    }

    [Fact]
    public void CreateKernel_WithSourceGen_DiscoversPlugins()
    {
        var config = new ConfigurationBuilder().Build();

        var kernel = new Syringe()
            .UsingSourceGen()
            .UsingSemanticKernel(syringe => syringe
                .AddSemanticKernelPlugin<TestSourceGenPlugin>())
            .BuildServiceProvider(config)
            .GetRequiredService<IKernelFactory>()
            .CreateKernel();

        Assert.NotNull(kernel);
        Assert.Contains(kernel.Plugins, p => p.Name == nameof(TestSourceGenPlugin));
    }

    [Fact]
    public void CreateKernel_Parity_ReflectionAndSourceGenProduceSamePlugins()
    {
        var config = new ConfigurationBuilder().Build();

        // Build with reflection
        var reflectionKernel = new Syringe()
            .UsingReflection()
            .UsingSemanticKernel(syringe => syringe
                .AddSemanticKernelPlugin<TestSourceGenPlugin>())
            .BuildServiceProvider(config)
            .GetRequiredService<IKernelFactory>()
            .CreateKernel();

        // Build with source-gen
        var sourceGenKernel = new Syringe()
            .UsingSourceGen()
            .UsingSemanticKernel(syringe => syringe
                .AddSemanticKernelPlugin<TestSourceGenPlugin>())
            .BuildServiceProvider(config)
            .GetRequiredService<IKernelFactory>()
            .CreateKernel();

        var reflectionPluginNames = reflectionKernel.Plugins.Select(p => p.Name).OrderBy(n => n).ToList();
        var sourceGenPluginNames = sourceGenKernel.Plugins.Select(p => p.Name).OrderBy(n => n).ToList();

        Assert.Equal(reflectionPluginNames, sourceGenPluginNames);
    }
}

/// <summary>
/// Integration tests verifying real kernel plugins work with both strategies.
/// </summary>
public sealed class RealPluginIntegrationTests
{
    [Fact]
    public void KernelPlugin_WithRealFunctions_CanBeDiscoveredViaReflection()
    {
        var config = new ConfigurationBuilder().Build();

        var kernel = new Syringe()
            .UsingReflection()
            .UsingSemanticKernel(syringe => syringe
                .AddSemanticKernelPlugin<MathPlugin>())
            .BuildServiceProvider(config)
            .GetRequiredService<IKernelFactory>()
            .CreateKernel();

        Assert.Contains(kernel.Plugins, p => p.Name == nameof(MathPlugin));
        
        var mathPlugin = kernel.Plugins.First(p => p.Name == nameof(MathPlugin));
        Assert.Contains(mathPlugin, f => f.Name == "Add");
        Assert.Contains(mathPlugin, f => f.Name == "Multiply");
    }

    [Fact]
    public void KernelPlugin_WithRealFunctions_CanBeDiscoveredViaSourceGen()
    {
        var config = new ConfigurationBuilder().Build();

        var kernel = new Syringe()
            .UsingSourceGen()
            .UsingSemanticKernel(syringe => syringe
                .AddSemanticKernelPlugin<MathPlugin>())
            .BuildServiceProvider(config)
            .GetRequiredService<IKernelFactory>()
            .CreateKernel();

        Assert.Contains(kernel.Plugins, p => p.Name == nameof(MathPlugin));
        
        var mathPlugin = kernel.Plugins.First(p => p.Name == nameof(MathPlugin));
        Assert.Contains(mathPlugin, f => f.Name == "Add");
        Assert.Contains(mathPlugin, f => f.Name == "Multiply");
    }

    [Fact]
    public async Task KernelPlugin_FunctionsCanBeInvoked()
    {
        var config = new ConfigurationBuilder().Build();

        var kernel = new Syringe()
            .UsingSourceGen()
            .UsingSemanticKernel(syringe => syringe
                .AddSemanticKernelPlugin<MathPlugin>())
            .BuildServiceProvider(config)
            .GetRequiredService<IKernelFactory>()
            .CreateKernel();

        var mathPlugin = kernel.Plugins.First(p => p.Name == nameof(MathPlugin));
        var addFunction = mathPlugin.First(f => f.Name == "Add");
        
        var result = await addFunction.InvokeAsync<int>(kernel, new KernelArguments { ["a"] = 2, ["b"] = 3 }, TestContext.Current.CancellationToken);
        Assert.Equal(5, result);
    }

    [Fact]
    public void MultiplePlugins_AllDiscoveredCorrectly()
    {
        var config = new ConfigurationBuilder().Build();

        var kernel = new Syringe()
            .UsingSourceGen()
            .UsingSemanticKernel(syringe => syringe
                .AddSemanticKernelPlugin<MathPlugin>()
                .AddSemanticKernelPlugin<StringPlugin>())
            .BuildServiceProvider(config)
            .GetRequiredService<IKernelFactory>()
            .CreateKernel();

        Assert.Equal(2, kernel.Plugins.Count);
        Assert.Contains(kernel.Plugins, p => p.Name == nameof(MathPlugin));
        Assert.Contains(kernel.Plugins, p => p.Name == nameof(StringPlugin));
    }

    [Fact]
    public void StaticPlugin_CanBeDiscovered()
    {
        var config = new ConfigurationBuilder().Build();

        var kernel = new Syringe()
            .UsingSourceGen()
            .UsingSemanticKernel(syringe => syringe
                .AddSemanticKernelPluginsFromAssemblies(
                    includeInstancePlugins: false,
                    includeStaticPlugins: true))
            .BuildServiceProvider(config)
            .GetRequiredService<IKernelFactory>()
            .CreateKernel();

        Assert.Contains(kernel.Plugins, p => p.Name.Contains("Static"));
    }

    [Fact]
    public async Task PluginWithDependencies_ResolvesDependenciesCorrectly()
    {
        var config = new ConfigurationBuilder().Build();

        var kernel = new Syringe()
            .UsingSourceGen()
            .UsingSemanticKernel(syringe => syringe
                .AddSemanticKernelPlugin<PluginWithDependency>())
            .UsingPostPluginRegistrationCallback(svc => svc.AddSingleton<PluginDependency>())
            .BuildServiceProvider(config)
            .GetRequiredService<IKernelFactory>()
            .CreateKernel();

        var plugin = kernel.Plugins.First(p => p.Name == nameof(PluginWithDependency));
        var function = plugin.First(f => f.Name == "GetDependencyValue");
        
        var result = await function.InvokeAsync<string>(kernel, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("Dependency Value", result);
    }
}

public sealed class TestSourceGenPlugin
{
    [KernelFunction("TestFunction")]
    [Description("Test function for source-gen")]
    public string TestFunction() => "Source Gen Test";
}

public sealed class MathPlugin
{
    [KernelFunction("Add")]
    [Description("Adds two numbers")]
    public int Add(int a, int b) => a + b;

    [KernelFunction("Multiply")]
    [Description("Multiplies two numbers")]
    public int Multiply(int a, int b) => a * b;
}

public sealed class StringPlugin
{
    [KernelFunction("Reverse")]
    [Description("Reverses a string")]
    public string Reverse(string input) => new string(input.Reverse().ToArray());

    [KernelFunction("Length")]
    [Description("Gets the length of a string")]
    public int Length(string input) => input.Length;
}

public static class StaticMathPlugin
{
    [KernelFunction("StaticAdd")]
    [Description("Adds two numbers (static)")]
    public static int Add(int a, int b) => a + b;
}

public sealed class PluginDependency
{
    public string GetValue() => "Dependency Value";
}

public sealed class PluginWithDependency
{
    private readonly PluginDependency _dependency;

    public PluginWithDependency(PluginDependency dependency)
    {
        _dependency = dependency;
    }

    [KernelFunction("GetDependencyValue")]
    [Description("Gets value from dependency")]
    public string GetDependencyValue() => _dependency.GetValue();
}
