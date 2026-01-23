using NexusLabs.Needlr.Generators;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests;

public sealed class GenerateTypeRegistryAttributeTests
{
    [Fact]
    public void Constructor_Default_HasNullNamespacePrefixes()
    {
        // Act
        var attribute = new GenerateTypeRegistryAttribute();

        // Assert
        Assert.Null(attribute.IncludeNamespacePrefixes);
    }

    [Fact]
    public void Constructor_Default_IncludesSelf()
    {
        // Act
        var attribute = new GenerateTypeRegistryAttribute();

        // Assert
        Assert.True(attribute.IncludeSelf);
    }

    [Fact]
    public void IncludeNamespacePrefixes_CanBeSet()
    {
        // Arrange
        var prefixes = new[] { "NexusLabs", "MyCompany" };

        // Act
        var attribute = new GenerateTypeRegistryAttribute
        {
            IncludeNamespacePrefixes = prefixes
        };

        // Assert
        Assert.Equal(prefixes, attribute.IncludeNamespacePrefixes);
    }

    [Fact]
    public void IncludeSelf_CanBeSetToFalse()
    {
        // Act
        var attribute = new GenerateTypeRegistryAttribute
        {
            IncludeSelf = false
        };

        // Assert
        Assert.False(attribute.IncludeSelf);
    }

    [Fact]
    public void Attribute_HasCorrectUsage()
    {
        // Assert
        var usage = typeof(GenerateTypeRegistryAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        Assert.Equal(AttributeTargets.Assembly, usage.ValidOn);
        Assert.False(usage.AllowMultiple);
    }
}
