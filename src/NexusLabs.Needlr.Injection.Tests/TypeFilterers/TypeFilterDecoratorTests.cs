using NexusLabs.Needlr.Injection.TypeFilterers;

using Xunit;

namespace NexusLabs.Needlr.Injection.Tests.TypeFilterers;

public sealed class TypeFilterDecoratorTests
{
    [Fact]
    public void Constructor_WithNullInnerFilterer_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new TypeFilterDecorator(
                null!,
                t => true,
                t => true,
                t => true));
    }

    [Fact]
    public void Constructor_WithNullTypeFilter_ThrowsArgumentNullException()
    {
        var innerFilterer = new DefaultTypeFilterer();
        
        Assert.Throws<ArgumentNullException>(() =>
            new TypeFilterDecorator(
                innerFilterer,
                null!,
                t => true,
                t => true));
    }

    [Fact]
    public void Constructor_WithNullScopedTypeFilter_ThrowsArgumentNullException()
    {
        var innerFilterer = new DefaultTypeFilterer();
        
        Assert.Throws<ArgumentNullException>(() =>
            new TypeFilterDecorator(
                innerFilterer,
                t => true,
                null!,
                t => true));
    }

    [Fact]
    public void Constructor_WithNullSingletonTypeFilter_ThrowsArgumentNullException()
    {
        var innerFilterer = new DefaultTypeFilterer();
        
        Assert.Throws<ArgumentNullException>(() =>
            new TypeFilterDecorator(
                innerFilterer,
                t => true,
                t => true,
                null!));
    }

    [Fact]
    public void IsInjectableType_AppliesBothInnerAndOuterFilters()
    {
        var innerFilterer = new DefaultTypeFilterer();
        var callCount = 0;
        var decorator = new TypeFilterDecorator(
            innerFilterer,
            t => { callCount++; return true; },
            t => true,
            t => true);
        
        var result = decorator.IsInjectableType(typeof(SimpleClass));
        
        Assert.True(result);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void IsInjectableType_WhenInnerFilterReturnsFalse_ReturnsFalse()
    {
        var innerFilterer = new DefaultTypeFilterer();
        var decorator = new TypeFilterDecorator(
            innerFilterer,
            t => true,
            t => true,
            t => true);
        
        var result = decorator.IsInjectableType(typeof(AbstractClass));
        
        Assert.False(result);
    }

    [Fact]
    public void IsInjectableType_WhenOuterFilterReturnsFalse_ReturnsFalse()
    {
        var innerFilterer = new DefaultTypeFilterer();
        var decorator = new TypeFilterDecorator(
            innerFilterer,
            t => false,
            t => true,
            t => true);
        
        var result = decorator.IsInjectableType(typeof(SimpleClass));
        
        Assert.False(result);
    }

    [Fact]
    public void IsInjectableTransientType_AppliesBothInnerAndScopedFilters()
    {
        var innerFilterer = new DefaultTypeFilterer();
        var wasFilterCalled = false;
        var decorator = new TypeFilterDecorator(
            innerFilterer,
            t => true,
            t => { wasFilterCalled = true; return false; },
            t => true);
        
        var result = decorator.IsInjectableTransientType(typeof(SimpleClass));
        
        Assert.False(result);
        Assert.False(wasFilterCalled);
    }

    [Fact]
    public void IsInjectableSingletonType_AppliesBothInnerAndSingletonFilters()
    {
        var innerFilterer = new DefaultTypeFilterer();
        var callCount = 0;
        var decorator = new TypeFilterDecorator(
            innerFilterer,
            t => true,
            t => true,
            t => { callCount++; return true; });
        
        var result = decorator.IsInjectableSingletonType(typeof(SimpleClass));
        
        Assert.True(result);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void IsInjectableSingletonType_WhenSingletonFilterReturnsFalse_ReturnsFalse()
    {
        var innerFilterer = new DefaultTypeFilterer();
        var decorator = new TypeFilterDecorator(
            innerFilterer,
            t => true,
            t => true,
            t => false);
        
        var result = decorator.IsInjectableSingletonType(typeof(SimpleClass));
        
        Assert.False(result);
    }

    [Fact]
    public void FiltersCanBeChained()
    {
        var innerFilterer = new DefaultTypeFilterer();
        var firstDecorator = new TypeFilterDecorator(
            innerFilterer,
            t => t.Name.Length > 8,
            t => true,
            t => true);
        
        var secondDecorator = new TypeFilterDecorator(
            firstDecorator,
            t => t.Name.StartsWith("S"),
            t => true,
            t => true);
        
        var result1 = secondDecorator.IsInjectableType(typeof(SimpleClass));
        var result2 = secondDecorator.IsInjectableType(typeof(ShortClass));
        
        Assert.True(result1);
        Assert.True(result2);
    }

}