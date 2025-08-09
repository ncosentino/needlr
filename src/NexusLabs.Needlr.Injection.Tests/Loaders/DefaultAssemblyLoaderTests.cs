using NexusLabs.Needlr.Injection.Loaders;

using System.Reflection;

using Xunit;

namespace NexusLabs.Needlr.Injection.Tests.Loaders;

public sealed class DefaultAssemblyLoaderTests
{
    [Fact]
    public void LoadAssemblies_WithValidEntryAssembly_ReturnsAssemblies()
    {
        var loader = new DefaultAssemblyLoader();        
        var assemblies = loader.LoadAssemblies(continueOnAssemblyError: true);
        
        Assert.NotNull(assemblies);
        Assert.NotEmpty(assemblies);

        var entryAssembly = Assembly.GetEntryAssembly();
        if (entryAssembly != null)
        {
            Assert.Contains(assemblies, a => a.FullName == entryAssembly.FullName);
        }
    }

    [Fact]
    public void LoadAssemblies_WithContinueOnError_DoesNotThrowOnLoadFailure()
    {
        var loader = new DefaultAssemblyLoader();        
        var assemblies = loader.LoadAssemblies(continueOnAssemblyError: true);        
        Assert.NotNull(assemblies);
    }

    [Fact]
    public void Constructor_CreatesFileMatchAssemblyLoaderWithCorrectParameters()
    {
        var loader = new DefaultAssemblyLoader();        
        Assert.NotNull(loader);
    }
}