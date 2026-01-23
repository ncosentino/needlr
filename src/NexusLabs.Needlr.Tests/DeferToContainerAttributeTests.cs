using Xunit;

namespace NexusLabs.Needlr.Tests;

public sealed class DeferToContainerAttributeTests
{
    [Fact]
    public void Constructor_WithNoParameters_CreatesEmptyArray()
    {
        // Act
        var attribute = new DeferToContainerAttribute();

        // Assert
        Assert.NotNull(attribute.ConstructorParameterTypes);
        Assert.Empty(attribute.ConstructorParameterTypes);
    }

    [Fact]
    public void Constructor_WithSingleType_StoresType()
    {
        // Act
        var attribute = new DeferToContainerAttribute(typeof(string));

        // Assert
        Assert.Single(attribute.ConstructorParameterTypes);
        Assert.Equal(typeof(string), attribute.ConstructorParameterTypes[0]);
    }

    [Fact]
    public void Constructor_WithMultipleTypes_StoresAllTypes()
    {
        // Arrange
        var types = new[] { typeof(string), typeof(int), typeof(IDisposable) };

        // Act
        var attribute = new DeferToContainerAttribute(types);

        // Assert
        Assert.Equal(3, attribute.ConstructorParameterTypes.Length);
        Assert.Equal(typeof(string), attribute.ConstructorParameterTypes[0]);
        Assert.Equal(typeof(int), attribute.ConstructorParameterTypes[1]);
        Assert.Equal(typeof(IDisposable), attribute.ConstructorParameterTypes[2]);
    }

    [Fact]
    public void Constructor_WithNullArray_CreatesEmptyArray()
    {
        // Act
        var attribute = new DeferToContainerAttribute(null!);

        // Assert
        Assert.NotNull(attribute.ConstructorParameterTypes);
        Assert.Empty(attribute.ConstructorParameterTypes);
    }

    [Fact]
    public void Attribute_HasCorrectUsage()
    {
        // Assert
        var usage = typeof(DeferToContainerAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        Assert.Equal(AttributeTargets.Class, usage.ValidOn);
        Assert.False(usage.Inherited);
        Assert.False(usage.AllowMultiple);
    }
}
