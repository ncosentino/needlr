using NexusLabs.Needlr.Injection.Sorters;

using System.Reflection;

using Xunit;

namespace NexusLabs.Needlr.Injection.Tests.Sorters;

public sealed class DefaultAssemblySorterTests
{
    [Fact]
    public void Sort_WithNullAssemblies_ThrowsArgumentNullException()
    {
        var sorter = new DefaultAssemblySorter();
        
        Assert.Throws<ArgumentNullException>(() => sorter.Sort(null!).ToList());
    }

    [Fact]
    public void Sort_WithEmptyList_ReturnsEmpty()
    {
        var sorter = new DefaultAssemblySorter();
        var assemblies = new List<Assembly>();
        
        var sorted = sorter.Sort(assemblies).ToList();
        
        Assert.Empty(sorted);
    }

    [Fact]
    public void Sort_WithSingleAssembly_ReturnsSameAssembly()
    {
        var sorter = new DefaultAssemblySorter();
        var assembly = Assembly.GetExecutingAssembly();
        var assemblies = new[] { assembly };
        
        var sorted = sorter.Sort(assemblies).ToList();
        
        Assert.Single(sorted);
        Assert.Same(assembly, sorted[0]);
    }

    [Fact]
    public void Sort_WithMultipleAssemblies_ReturnsInOriginalOrder()
    {
        var sorter = new DefaultAssemblySorter();
        var assembly1 = Assembly.GetExecutingAssembly();
        var assembly2 = typeof(object).Assembly;
        var assembly3 = typeof(Xunit.Assert).Assembly;
        var assemblies = new[] { assembly1, assembly2, assembly3 };
        
        var sorted = sorter.Sort(assemblies).ToList();
        
        Assert.Equal(3, sorted.Count);
        Assert.Same(assembly1, sorted[0]);
        Assert.Same(assembly2, sorted[1]);
        Assert.Same(assembly3, sorted[2]);
    }

    [Fact]
    public void Sort_DoesNotModifyInputList()
    {
        var sorter = new DefaultAssemblySorter();
        var originalAssemblies = new[] 
        { 
            Assembly.GetExecutingAssembly(),
            typeof(object).Assembly 
        };
        var assembliesCopy = originalAssemblies.ToArray();
        
        var sorted = sorter.Sort(originalAssemblies).ToList();
        
        Assert.Equal(originalAssemblies.Length, assembliesCopy.Length);
        for (int i = 0; i < originalAssemblies.Length; i++)
        {
            Assert.Same(originalAssemblies[i], assembliesCopy[i]);
        }
    }
}