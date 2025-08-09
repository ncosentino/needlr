using NexusLabs.Needlr.Injection.Sorters;

using System.Reflection;

using Xunit;

namespace NexusLabs.Needlr.Injection.Tests.Sorters;

public sealed class AlphabeticalAssemblySorterTests
{
    [Fact]
    public void Sort_WithNullAssemblies_ThrowsArgumentNullException()
    {
        var sorter = new AlphabeticalAssemblySorter();
        
        Assert.Throws<ArgumentNullException>(() => sorter.Sort(null!).ToList());
    }

    [Fact]
    public void Sort_WithEmptyList_ReturnsEmpty()
    {
        var sorter = new AlphabeticalAssemblySorter();
        var assemblies = new List<Assembly>();
        
        var sorted = sorter.Sort(assemblies).ToList();
        
        Assert.Empty(sorted);
    }

    [Fact]
    public void Sort_WithSingleAssembly_ReturnsSameAssembly()
    {
        var sorter = new AlphabeticalAssemblySorter();
        var assembly = Assembly.GetExecutingAssembly();
        var assemblies = new[] { assembly };
        
        var sorted = sorter.Sort(assemblies).ToList();
        
        Assert.Single(sorted);
        Assert.Same(assembly, sorted[0]);
    }

    [Fact]
    public void Sort_WithMultipleAssemblies_SortsByLocation()
    {
        var sorter = new AlphabeticalAssemblySorter();
        var assemblies = new[]
        {
            Assembly.GetExecutingAssembly(),
            typeof(Syringe).Assembly,
            typeof(Xunit.Assert).Assembly
        }.Where(a => !string.IsNullOrEmpty(a.Location)).ToArray();

        var sorted = sorter.Sort(assemblies).ToList();
        Assert.Equal(assemblies.Length, sorted.Count);

        Assert.Equal(
            assemblies.OrderBy(a => a.Location).Select(x => x.Location),
            sorted.Select(x => x.Location));
    }

    [Fact]
    public void Sort_PreservesAllAssemblies()
    {
        var sorter = new AlphabeticalAssemblySorter();
        var assemblies = new[]
        {
            Assembly.GetExecutingAssembly(),
            typeof(object).Assembly,
            typeof(Xunit.Assert).Assembly
        };
        
        var sorted = sorter.Sort(assemblies).ToList();
        
        Assert.Equal(assemblies.Length, sorted.Count);
        foreach (var assembly in assemblies)
        {
            Assert.Contains(assembly, sorted);
        }
    }
}