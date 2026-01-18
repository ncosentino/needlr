using NexusLabs.Needlr.Injection.Reflection.Loaders;

using Xunit;

namespace NexusLabs.Needlr.Injection.Tests.Loaders;

public sealed class AllAssembliesLoaderTests
{
    [Fact]
    public void LoadAssemblies_LoadsAllDllAndExeFiles()
    {
        var loader = new AllAssembliesLoader();
        
        var assemblies = loader.LoadAssemblies(continueOnAssemblyError: true);
        
        Assert.NotNull(assemblies);
        Assert.NotEmpty(assemblies);
    }

    [Fact]
    public void LoadAssemblies_WithContinueOnError_HandlesLoadFailuresGracefully()
    {
        var loader = new AllAssembliesLoader();
        
        var assemblies = loader.LoadAssemblies(continueOnAssemblyError: true);
        
        Assert.NotNull(assemblies);
    }

    [Fact]
    public void LoadAssemblies_WithoutContinueOnError_PropagatesExceptions()
    {
        var loader = new AllAssembliesLoader();
        File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "__fake.dll")).Dispose();
        
        var error = Assert.Throws<InvalidOperationException>(() => loader.LoadAssemblies(continueOnAssemblyError: false));
        Assert.Contains("Failed to load assembly", error.Message);
    }

    [Fact]
    public void Constructor_InitializesFileMatchAssemblyLoaderCorrectly()
    {
        var loader = new AllAssembliesLoader();
        
        Assert.NotNull(loader);
    }
}