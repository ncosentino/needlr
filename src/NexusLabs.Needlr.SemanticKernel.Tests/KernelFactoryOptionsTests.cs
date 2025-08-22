using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using NexusLabs.Needlr.SemanticKernel;
using Xunit;

namespace NexusLabs.Needlr.SemanticKernel.Tests;

public class KernelFactoryOptionsTests
{
    [Fact]
    public void Constructor_WithServiceProviderAndKernelBuilder_InitializesAllProperties()
    {
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var kernelBuilder = Kernel.CreateBuilder();
        
        var options = new KernelFactoryOptions(serviceProvider, kernelBuilder);
        
        Assert.NotNull(options.ServiceProvider);
        Assert.NotNull(options.KernelBuilder);
        Assert.Same(serviceProvider, options.ServiceProvider);
        Assert.Same(kernelBuilder, options.KernelBuilder);
    }
    
    [Fact]
    public void Equals_TwoInstancesWithSameValues_AreEqual()
    {
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var kernelBuilder = Kernel.CreateBuilder();
        
        var options1 = new KernelFactoryOptions(serviceProvider, kernelBuilder);
        var options2 = new KernelFactoryOptions(serviceProvider, kernelBuilder);
        
        Assert.Equal(options1, options2);
    }
    
    [Fact]
    public void WithExpression_ModifyingServiceProvider_CreatesNewInstanceWithUpdatedValue()
    {
        var services = new ServiceCollection();
        var serviceProvider1 = services.BuildServiceProvider();
        var serviceProvider2 = services.BuildServiceProvider();
        var kernelBuilder = Kernel.CreateBuilder();
        
        var options = new KernelFactoryOptions(serviceProvider1, kernelBuilder);
        
        var newOptions = options with { ServiceProvider = serviceProvider2 };
        
        Assert.NotSame(options, newOptions);
        Assert.Same(serviceProvider2, newOptions.ServiceProvider);
        Assert.Same(kernelBuilder, newOptions.KernelBuilder);
    }
}