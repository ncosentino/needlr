using Xunit;

namespace NexusLabs.Needlr.Injection.Tests;

public sealed class TypeFilteringTests
{
    [Fact]
    public void IsConcreteType_WithConcreteClass_ReturnsTrue()
    {
        var result = TypeFiltering.IsConcreteType(typeof(ConcreteClass));        
        Assert.True(result);
    }

    [Fact]
    public void IsConcreteType_WithAbstractClass_ReturnsFalse()
    {
        var result = TypeFiltering.IsConcreteType(typeof(AbstractTestClass));        
        Assert.False(result);
    }

    [Fact]
    public void IsConcreteType_WithInterface_ReturnsFalse()
    {
        var result = TypeFiltering.IsConcreteType(typeof(IFilterTestInterface));        
        Assert.False(result);
    }

    [Fact]
    public void IsConcreteType_WithGenericTypeDefinition_ReturnsFalse()
    {
        var result = TypeFiltering.IsConcreteType(typeof(GenericClass<>));        
        Assert.False(result);
    }

    [Fact]
    public void IsConcreteType_WithClosedGenericType_ReturnsTrue()
    {
        var result = TypeFiltering.IsConcreteType(typeof(GenericClass<int>));        
        Assert.True(result);
    }

    [Fact]
    public void IsConcreteType_WithValueType_ReturnsFalse()
    {
        var result = TypeFiltering.IsConcreteType(typeof(int));        
        Assert.False(result);
    }

    [Fact]
    public void IsConcreteType_WithStruct_ReturnsFalse()
    {
        var result = TypeFiltering.IsConcreteType(typeof(TestStruct));        
        Assert.False(result);
    }

    [Fact]
    public void IsConcreteType_WithEnum_ReturnsFalse()
    {
        var result = TypeFiltering.IsConcreteType(typeof(TestEnum));        
        Assert.False(result);
    }

    [Fact]
    public void IsConcreteType_WithNestedClass_ReturnsFalse()
    {
        var result = TypeFiltering.IsConcreteType(typeof(OuterClass.NestedClass));        
        Assert.False(result);
    }

    [Fact]
    public void IsConcreteType_WithDelegate_ReturnsFalse()
    {
        var result = TypeFiltering.IsConcreteType(typeof(Action));        
        Assert.False(result);
    }

    [Fact]
    public void IsConcreteType_WithException_ReturnsFalse()
    {
        var result = TypeFiltering.IsConcreteType(typeof(TestException));        
        Assert.False(result);
    }

    [Fact]
    public void IsConcreteType_WithAttribute_ReturnsFalse()
    {
        var result = TypeFiltering.IsConcreteType(typeof(TestAttribute));        
        Assert.False(result);
    }

    [Fact]
    public void IsConcreteType_WithRecord_ReturnsFalse()
    {
        var result = TypeFiltering.IsConcreteType(typeof(TestRecord));        
        Assert.False(result);
    }

    [Fact]
    public void IsConcreteType_WithCompilerGeneratedClass_ReturnsFalse()
    {
        var anonymousType = new { Name = "Test" }.GetType();
        
        var result = TypeFiltering.IsConcreteType(anonymousType);
        
        Assert.False(result);
    }

    [Fact]
    public void IsRecord_WithRecord_ReturnsTrue()
    {
        var result = TypeFiltering.IsRecord(typeof(TestRecord));        
        Assert.True(result);
    }

    [Fact]
    public void IsRecord_WithClass_ReturnsFalse()
    {
        var result = TypeFiltering.IsRecord(typeof(ConcreteClass));        
        Assert.False(result);
    }

    [Fact]
    public void IsRecord_WithStruct_ReturnsFalse()
    {
        var result = TypeFiltering.IsRecord(typeof(TestStruct));        
        Assert.False(result);
    }
}
