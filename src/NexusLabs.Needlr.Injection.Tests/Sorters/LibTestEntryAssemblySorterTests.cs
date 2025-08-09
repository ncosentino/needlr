using NexusLabs.Needlr.Injection.Sorters;

using System.Reflection;

using Xunit;

namespace NexusLabs.Needlr.Injection.Tests.Sorters;

public sealed class LibTestEntryAssemblySorterTests
{
    [Fact]
    public void Constructor_WithNullIsTestAssembly_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new LibTestEntryAssemblySorter(
                null!,
                a => true,
                a => false));
    }

    [Fact]
    public void Constructor_WithNullIsClassLibrary_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new LibTestEntryAssemblySorter(
                a => false,
                null!,
                a => false));
    }

    [Fact]
    public void Constructor_WithNullIsEntryPoint_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new LibTestEntryAssemblySorter(
                a => false,
                a => true,
                null!));
    }

    [Fact]
    public void Sort_WithEmptyList_ReturnsEmpty()
    {
        var sorter = new LibTestEntryAssemblySorter(
            a => false,
            a => true,
            a => false);
        var assemblies = new List<Assembly>();
        
        var sorted = sorter.Sort(assemblies).ToList();
        
        Assert.Empty(sorted);
    }

    [Fact]
    public void Sort_WithOnlyLibraries_ReturnsAllLibraries()
    {
        var lib1 = Assembly.GetExecutingAssembly();
        var lib2 = typeof(object).Assembly;
        
        var sorter = new LibTestEntryAssemblySorter(
            a => false,
            a => true,
            a => false);
        
        var assemblies = new[] { lib1, lib2 };
        
        var sorted = sorter.Sort(assemblies).ToList();
        
        Assert.Equal(2, sorted.Count);
        Assert.Contains(lib1, sorted);
        Assert.Contains(lib2, sorted);
    }

    [Fact]
    public void Sort_WithOnlyTestAssemblies_ReturnsAllTests()
    {
        var test1 = Assembly.GetExecutingAssembly();
        var test2 = typeof(Xunit.Assert).Assembly;
        
        var sorter = new LibTestEntryAssemblySorter(
            a => true,
            a => false,
            a => false);
        
        var assemblies = new[] { test1, test2 };
        
        var sorted = sorter.Sort(assemblies).ToList();
        
        Assert.Equal(2, sorted.Count);
        Assert.Contains(test1, sorted);
        Assert.Contains(test2, sorted);
    }

    [Fact]
    public void Sort_WithMixedTypes_CategoriesCorrectly()
    {
        var assemblies = new[]
        {
            Assembly.GetExecutingAssembly(),
            typeof(object).Assembly,
            typeof(Xunit.Assert).Assembly
        };
        
        var sorter = new LibTestEntryAssemblySorter(
            a => a.GetName().Name?.Contains("Test") ?? false,
            a => !a.GetName().Name?.Contains("Test") ?? true,
            a => a.EntryPoint != null);
        
        var sorted = sorter.Sort(assemblies).ToList();
        
        Assert.Equal(assemblies.Length, sorted.Count);
        foreach (var assembly in assemblies)
        {
            Assert.Contains(assembly, sorted);
        }
    }

    [Fact]
    public void Sort_PreservesAllAssembliesRegardlessOfCategory()
    {
        var assemblies = new[]
        {
            Assembly.GetExecutingAssembly(),
            typeof(object).Assembly,
            typeof(Xunit.Assert).Assembly
        };
        
        var sorter = new LibTestEntryAssemblySorter(
            a => false,
            a => false,
            a => false);
        
        var sorted = sorter.Sort(assemblies).ToList();
        
        Assert.Empty(sorted);
    }
}