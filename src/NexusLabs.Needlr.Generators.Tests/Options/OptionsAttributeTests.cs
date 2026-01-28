using Xunit;

using NexusLabs.Needlr.Generators;

namespace NexusLabs.Needlr.Generators.Tests.Options;

/// <summary>
/// Tests for the <see cref="OptionsAttribute"/> class.
/// </summary>
public sealed class OptionsAttributeTests
{
    [Fact]
    public void OptionsAttribute_WithSectionName_StoresSectionName()
    {
        var attr = new OptionsAttribute("Database");
        
        Assert.Equal("Database", attr.SectionName);
    }

    [Fact]
    public void OptionsAttribute_WithoutSectionName_SectionNameIsNull()
    {
        var attr = new OptionsAttribute();
        
        Assert.Null(attr.SectionName);
    }

    [Fact]
    public void OptionsAttribute_ValidateOnStart_DefaultsToFalse()
    {
        var attr = new OptionsAttribute();
        
        Assert.False(attr.ValidateOnStart);
    }

    [Fact]
    public void OptionsAttribute_ValidateOnStart_CanBeSet()
    {
        var attr = new OptionsAttribute { ValidateOnStart = true };
        
        Assert.True(attr.ValidateOnStart);
    }

    [Fact]
    public void OptionsAttribute_Name_DefaultsToNull()
    {
        var attr = new OptionsAttribute("Section");
        
        Assert.Null(attr.Name);
    }

    [Fact]
    public void OptionsAttribute_Name_CanBeSet()
    {
        var attr = new OptionsAttribute("Databases:Primary") { Name = "Primary" };
        
        Assert.Equal("Primary", attr.Name);
    }

    [Fact]
    public void OptionsAttribute_AllowsMultiple()
    {
        var attrUsage = typeof(OptionsAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();
        
        Assert.True(attrUsage.AllowMultiple);
    }

    [Fact]
    public void OptionsAttribute_TargetsClassOnly()
    {
        var attrUsage = typeof(OptionsAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();
        
        Assert.Equal(AttributeTargets.Class, attrUsage.ValidOn);
    }
}
