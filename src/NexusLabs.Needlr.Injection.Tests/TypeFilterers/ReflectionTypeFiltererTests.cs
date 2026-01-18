using NexusLabs.Needlr.Injection.TypeFilterers;

using Xunit;

namespace NexusLabs.Needlr.Injection.Tests.TypeFilterers;

public sealed class ReflectionTypeFiltererTests
{
    private readonly ReflectionTypeFilterer _filterer = new();

    [Fact]
    public void IsInjectableScopedType_AlwaysReturnsFalse()
    {
        var result = _filterer.IsInjectableTransientType(typeof(SimpleClass));        
        Assert.False(result);
    }

    [Fact]
    public void IsInjectableTransientType_AlwaysReturnsFalse()
    {
        var result = _filterer.IsInjectableTransientType(typeof(SimpleClass));
        Assert.False(result);
    }

    [Fact]
    public void IsInjectableSingletonType_WithAbstractClass_ReturnsFalse()
    {
        var result = _filterer.IsInjectableSingletonType(typeof(AbstractClass));        
        Assert.False(result);
    }

    [Fact]
    public void IsInjectableSingletonType_WithInterface_ReturnsFalse()
    {
        var result = _filterer.IsInjectableSingletonType(typeof(ITestInterface));        
        Assert.False(result);
    }

    [Fact]
    public void IsInjectableSingletonType_WithValueType_ReturnsFalse()
    {
        var result = _filterer.IsInjectableSingletonType(typeof(int));        
        Assert.False(result);
    }

    [Fact]
    public void IsInjectableSingletonType_WithDoNotInjectAttribute_ReturnsFalse()
    {
        var result = _filterer.IsInjectableSingletonType(typeof(DoNotInjectClass));        
        Assert.False(result);
    }

    [Fact]
    public void IsInjectableSingletonType_WithParameterlessConstructor_ReturnsTrue()
    {
        var result = _filterer.IsInjectableSingletonType(typeof(SimpleClass));        
        Assert.True(result);
    }

    [Fact]
    public void IsInjectableSingletonType_WithInjectableParameters_ReturnsTrue()
    {
        var result = _filterer.IsInjectableSingletonType(typeof(ClassWithDependencies));        
        Assert.True(result);
    }

    [Fact]
    public void IsInjectableSingletonType_WithSelfReferencingConstructor_ReturnsFalse()
    {
        var result = _filterer.IsInjectableSingletonType(typeof(SelfReferencingClass));        
        Assert.False(result);
    }

    [Fact]
    public void IsInjectableSingletonType_WithValueTypeParameter_ReturnsFalse()
    {
        var result = _filterer.IsInjectableSingletonType(typeof(ClassWithValueTypeConstructor));        
        Assert.False(result);
    }

    [Fact]
    public void IsInjectableSingletonType_WithStringParameter_ReturnsFalse()
    {
        var result = _filterer.IsInjectableSingletonType(typeof(ClassWithStringConstructor));        
        Assert.False(result);
    }

    [Fact]
    public void IsInjectableSingletonType_WithDelegateParameter_ReturnsFalse()
    {
        var result = _filterer.IsInjectableSingletonType(typeof(ClassWithDelegateConstructor));        
        Assert.False(result);
    }

    [Fact]
    public void IsInjectableSingletonType_WithPrivateParameterlessConstructor_ReturnsTrue()
    {
        var result = _filterer.IsInjectableSingletonType(typeof(ClassWithPrivateConstructor));        
        Assert.True(result);
    }

    [Fact]
    public void IsInjectableSingletonType_WithMultipleConstructors_ChecksAllConstructors()
    {
        var result = _filterer.IsInjectableSingletonType(typeof(ClassWithMultipleConstructors));        
        Assert.True(result);
    }
}