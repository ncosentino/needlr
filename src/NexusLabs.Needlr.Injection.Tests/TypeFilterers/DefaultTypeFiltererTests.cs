using NexusLabs.Needlr.Injection.TypeFilterers;

using Xunit;

namespace NexusLabs.Needlr.Injection.Tests.TypeFilterers;

public sealed class DefaultTypeFiltererTests
{
    private readonly DefaultTypeFilterer _filterer = new();

    [Fact]
    public void IsInjectableTransientType_AlwaysReturnsFalse()
    {
        var result = _filterer.IsInjectableTransientType(typeof(SimpleClass));        
        Assert.False(result);
    }

    [Fact]
    public void IsInjectableSingletonType_CallsIsInjectableType()
    {
        var result = _filterer.IsInjectableSingletonType(typeof(SimpleClass));        
        Assert.True(result);
    }

    [Fact]
    public void IsInjectableType_WithAbstractClass_ReturnsFalse()
    {
        var result = _filterer.IsInjectableType(typeof(AbstractClass));        
        Assert.False(result);
    }

    [Fact]
    public void IsInjectableType_WithInterface_ReturnsFalse()
    {
        var result = _filterer.IsInjectableType(typeof(ITestInterface));        
        Assert.False(result);
    }

    [Fact]
    public void IsInjectableType_WithValueType_ReturnsFalse()
    {
        var result = _filterer.IsInjectableType(typeof(int));        
        Assert.False(result);
    }

    [Fact]
    public void IsInjectableType_WithDoNotInjectAttribute_ReturnsFalse()
    {
        var result = _filterer.IsInjectableType(typeof(DoNotInjectClass));        
        Assert.False(result);
    }

    [Fact]
    public void IsInjectableType_WithParameterlessConstructor_ReturnsTrue()
    {
        var result = _filterer.IsInjectableType(typeof(SimpleClass));        
        Assert.True(result);
    }

    [Fact]
    public void IsInjectableType_WithInjectableParameters_ReturnsTrue()
    {
        var result = _filterer.IsInjectableType(typeof(ClassWithDependencies));        
        Assert.True(result);
    }

    [Fact]
    public void IsInjectableType_WithSelfReferencingConstructor_ReturnsFalse()
    {
        var result = _filterer.IsInjectableType(typeof(SelfReferencingClass));        
        Assert.False(result);
    }

    [Fact]
    public void IsInjectableType_WithValueTypeParameter_ReturnsFalse()
    {
        var result = _filterer.IsInjectableType(typeof(ClassWithValueTypeConstructor));        
        Assert.False(result);
    }

    [Fact]
    public void IsInjectableType_WithStringParameter_ReturnsFalse()
    {
        var result = _filterer.IsInjectableType(typeof(ClassWithStringConstructor));        
        Assert.False(result);
    }

    [Fact]
    public void IsInjectableType_WithDelegateParameter_ReturnsFalse()
    {
        var result = _filterer.IsInjectableType(typeof(ClassWithDelegateConstructor));        
        Assert.False(result);
    }

    [Fact]
    public void IsInjectableType_WithPrivateParameterlessConstructor_ReturnsTrue()
    {
        var result = _filterer.IsInjectableType(typeof(ClassWithPrivateConstructor));        
        Assert.True(result);
    }

    [Fact]
    public void IsInjectableType_WithMultipleConstructors_ChecksAllConstructors()
    {
        var result = _filterer.IsInjectableType(typeof(ClassWithMultipleConstructors));        
        Assert.True(result);
    }
}