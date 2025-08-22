using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using NexusLabs.Needlr.SemanticKernel;
using Xunit;

namespace NexusLabs.Needlr.SemanticKernel.Tests;

public class KernelBuilderPluginOptionsTests
{
    [Fact]
    public void Constructor_WithKernelBuilder_InitializesKernelBuilderProperty()
    {
        var kernelBuilder = Kernel.CreateBuilder();
        
        var options = new KernelBuilderPluginOptions(kernelBuilder);
        
        Assert.NotNull(options.KernelBuilder);
        Assert.Same(kernelBuilder, options.KernelBuilder);
    }
    
    [Fact]
    public void KernelBuilder_ModifiedThroughOptions_ReflectsChangesInBuiltKernel()
    {
        var kernelBuilder = Kernel.CreateBuilder();
        var options = new KernelBuilderPluginOptions(kernelBuilder);
        
        options.KernelBuilder.Services.AddSingleton<TestService>();
        var kernel = kernelBuilder.Build();
        
        var service = kernel.Services.GetService<TestService>();
        Assert.NotNull(service);
    }
    
    private class TestService { }
}