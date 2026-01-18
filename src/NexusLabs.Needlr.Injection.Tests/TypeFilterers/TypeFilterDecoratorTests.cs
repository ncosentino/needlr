using NexusLabs.Needlr.Injection.Reflection.TypeFilterers;
using NexusLabs.Needlr.Injection.TypeFilterers;

using Xunit;

namespace NexusLabs.Needlr.Injection.Tests.TypeFilterers;

public sealed class TypeFilterDecoratorTests
{
    [Fact]
    public void IsInjectableScopedType_CallsProvidedFunction()
    {
        var innerFilterer = new EmptyTypeFilterer();
        var wasCalled = false;
        var decorator = new TypeFilterDecorator(
            innerFilterer,
            scopedTypeFilterer: (predicate, type) =>
            {
                wasCalled = true;
                return predicate(type) || type == typeof(SimpleClass);
            },
            transientTypeFilterer: (predicate, type) => predicate(type),
            singletonTypeFilter: (predicate, type) => predicate(type));
        
        var result = decorator.IsInjectableScopedType(typeof(SimpleClass));
        
        Assert.True(wasCalled);
        Assert.True(result);
    }

    [Fact]
    public void IsInjectableTransientType_CallsProvidedFunction()
    {
        var innerFilterer = new EmptyTypeFilterer();
        var wasCalled = false;
        var decorator = new TypeFilterDecorator(
            innerFilterer,
            scopedTypeFilterer: (predicate, type) => predicate(type),
            transientTypeFilterer: (predicate, type) =>
            {
                wasCalled = true;
                return predicate(type) || type == typeof(SimpleClass);
            },
            singletonTypeFilter: (predicate, type) => predicate(type));
        
        var result = decorator.IsInjectableTransientType(typeof(SimpleClass));
        
        Assert.True(wasCalled);
        Assert.True(result);
    }

    [Fact]
    public void IsInjectableSingletonType_CallsProvidedFunction()
    {
        var innerFilterer = new EmptyTypeFilterer();
        var wasCalled = false;
        var decorator = new TypeFilterDecorator(
            innerFilterer,
            scopedTypeFilterer: (predicate, type) => predicate(type),
            transientTypeFilterer: (predicate, type) => predicate(type),
            singletonTypeFilter: (predicate, type) =>
            {
                wasCalled = true;
                return predicate(type) || type == typeof(SimpleClass);
            });
        
        var result = decorator.IsInjectableSingletonType(typeof(SimpleClass));
        
        Assert.True(wasCalled);
        Assert.True(result);
    }

    [Fact]
    public void Decorator_PassesInnerFiltererPredicate()
    {
        var innerFilterer = new ReflectionTypeFilterer();
        Type? capturedType = null;
        
        var decorator = new TypeFilterDecorator(
            innerFilterer,
            scopedTypeFilterer: (predicate, type) =>
            {
                capturedType = type;
                return predicate(type);
            },
            transientTypeFilterer: (predicate, type) => predicate(type),
            singletonTypeFilter: (predicate, type) => predicate(type));
        
        var testType = typeof(SimpleClass);
        var result = decorator.IsInjectableScopedType(testType);
        
        Assert.Same(testType, capturedType);
        Assert.Equal(innerFilterer.IsInjectableScopedType(testType), result);
    }

    [Fact]
    public void Decorator_CanOverrideInnerFiltererDecision()
    {
        var innerFilterer = new EmptyTypeFilterer();
        
        var decorator = new TypeFilterDecorator(
            innerFilterer,
            scopedTypeFilterer: (predicate, type) => true,
            transientTypeFilterer: (predicate, type) => false,
            singletonTypeFilter: (predicate, type) => true);
        
        Assert.False(innerFilterer.IsInjectableScopedType(typeof(SimpleClass)));
        Assert.True(decorator.IsInjectableScopedType(typeof(SimpleClass)));
        
        Assert.False(innerFilterer.IsInjectableTransientType(typeof(SimpleClass)));
        Assert.False(decorator.IsInjectableTransientType(typeof(SimpleClass)));
        
        Assert.False(innerFilterer.IsInjectableSingletonType(typeof(SimpleClass)));
        Assert.True(decorator.IsInjectableSingletonType(typeof(SimpleClass)));
    }

    [Fact]
    public void IsInjectableScopedType_ComposedFilters_PredicatesCanBeChained()
    {
        var innerFilterer = new EmptyTypeFilterer();
        
        var firstDecorator = new TypeFilterDecorator(
            innerFilterer,
            scopedTypeFilterer: static (predicate, type) => 
                predicate(type) || type.Name.StartsWith('S'),
            transientTypeFilterer: (predicate, type) => predicate(type),
            singletonTypeFilter: (predicate, type) => predicate(type));
        
        var secondDecorator = new TypeFilterDecorator(
            firstDecorator,
            scopedTypeFilterer: (predicate, type) => 
                predicate(type) && type.Name.Length > 10,
            transientTypeFilterer: (predicate, type) => predicate(type),
            singletonTypeFilter: (predicate, type) => predicate(type));

        Assert.True(firstDecorator.IsInjectableScopedType(typeof(SimpleClass)));
        Assert.False(firstDecorator.IsInjectableScopedType(typeof(LongNamedClass)));
        Assert.True(firstDecorator.IsInjectableScopedType(typeof(ShortClass)));

        Assert.True(secondDecorator.IsInjectableScopedType(typeof(SimpleClass)));
        Assert.False(secondDecorator.IsInjectableScopedType(typeof(LongNamedClass)));
        Assert.False(secondDecorator.IsInjectableScopedType(typeof(ShortClass)));
    }

    private class SimpleClass { }
    private class ShortClass { }
    private class LongNamedClass { }
}