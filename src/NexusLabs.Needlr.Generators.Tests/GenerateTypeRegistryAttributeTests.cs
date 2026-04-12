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
    public void Constructor_Default_HasNullExcludeNamespacePrefixes()
    {
        var attribute = new GenerateTypeRegistryAttribute();
        Assert.Null(attribute.ExcludeNamespacePrefixes);
    }

    [Fact]
    public void ExcludeNamespacePrefixes_CanBeSet()
    {
        var prefixes = new[] { "Avalonia", "Microsoft.Maui" };
        var attribute = new GenerateTypeRegistryAttribute
        {
            ExcludeNamespacePrefixes = prefixes
        };
        Assert.Equal(prefixes, attribute.ExcludeNamespacePrefixes);
    }

    [Fact]
    public void BothIncludeAndExclude_CanBeSetSimultaneously()
    {
        var attribute = new GenerateTypeRegistryAttribute
        {
            IncludeNamespacePrefixes = new[] { "MyApp" },
            ExcludeNamespacePrefixes = new[] { "MyApp.Generated" }
        };
        Assert.Single(attribute.IncludeNamespacePrefixes!);
        Assert.Single(attribute.ExcludeNamespacePrefixes!);
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
