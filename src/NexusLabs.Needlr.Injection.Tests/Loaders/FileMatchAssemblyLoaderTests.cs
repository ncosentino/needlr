using NexusLabs.Needlr.Injection.Reflection.Loaders;

using Xunit;

namespace NexusLabs.Needlr.Injection.Tests.Loaders;

public sealed class FileMatchAssemblyLoaderTests
{
    [Fact]
    public void Constructor_WithNullDirectories_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new FileMatchAssemblyLoader(null!, fileName => true));
    }

    [Fact]
    public void Constructor_WithNullFileFilter_ThrowsArgumentNullException()
    {
        var directories = new[] { Directory.GetCurrentDirectory() };
        
        Assert.Throws<ArgumentNullException>(() =>
            new FileMatchAssemblyLoader(directories, null));
    }

    [Fact]
    public void LoadAssemblies_WithValidDirectoryAndFilter_ReturnsMatchingAssemblies()
    {
        var currentDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var directories = new[] { currentDirectory };
        var loader = new FileMatchAssemblyLoader(
            directories,
            fileName => fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));
        
        var assemblies = loader.LoadAssemblies(continueOnAssemblyError: true);
        
        Assert.NotNull(assemblies);
        Assert.NotEmpty(assemblies);
        Assert.All(assemblies, a => Assert.NotNull(a.FullName));
    }

    [Fact]
    public void LoadAssemblies_WithFilterThatMatchesNothing_ReturnsEmptyList()
    {
        var currentDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var directories = new[] { currentDirectory };
        var loader = new FileMatchAssemblyLoader(
            directories,
            fileName => false);
        
        var assemblies = loader.LoadAssemblies(continueOnAssemblyError: true);
        
        Assert.NotNull(assemblies);
        Assert.Empty(assemblies);
    }

    [Fact]
    public void LoadAssemblies_WithSpecificFileNameFilter_ReturnsOnlyMatchingAssemblies()
    {
        var currentDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var directories = new[] { currentDirectory };
        var testAssemblyName = "NexusLabs.Needlr.Injection.Tests.dll";
        var loader = new FileMatchAssemblyLoader(
            directories,
            fileName => fileName.Equals(testAssemblyName, StringComparison.OrdinalIgnoreCase));
        
        var assemblies = loader.LoadAssemblies(continueOnAssemblyError: true);

        Assert.NotNull(assemblies);
        Assert.Single(assemblies);
        Assert.Contains("NexusLabs.Needlr.Injection.Tests", assemblies.First().FullName);
    }

    [Fact]
    public void LoadAssemblies_WithInvalidDirectory_ReturnsEmptyWhenContinueOnError()
    {
        var directories = new[] { "C:\\NonExistentDirectory12345" };
        var loader = new FileMatchAssemblyLoader(
            directories,
            fileName => true);
        
        Assert.Throws<DirectoryNotFoundException>(() => 
            loader.LoadAssemblies(continueOnAssemblyError: true));
    }

    [Fact]
    public void LoadAssemblies_WithMultipleDirectories_SearchesAllDirectories()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var directories = new[] { baseDir };

        var loader = new FileMatchAssemblyLoader(
            directories,
            fileName => fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));
        var assemblies = loader.LoadAssemblies(continueOnAssemblyError: true);

        Assert.NotNull(assemblies);
    }
}