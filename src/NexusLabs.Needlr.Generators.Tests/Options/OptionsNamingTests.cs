using Xunit;

using NexusLabs.Needlr.Generators.Helpers;

namespace NexusLabs.Needlr.Generators.Tests.Options;

/// <summary>
/// Tests for <see cref="OptionsNamingHelper"/> section name inference.
/// </summary>
public sealed class OptionsNamingTests
{
    [Theory]
    [InlineData("DatabaseOptions", "Database")]
    [InlineData("CacheSettings", "Cache")]
    [InlineData("RedisConfig", "Redis")]
    [InlineData("FeatureFlags", "FeatureFlags")]
    [InlineData("Stripe", "Stripe")]
    [InlineData("MyAppOptions", "MyApp")]
    [InlineData("ConnectionSettings", "Connection")]
    [InlineData("LoggingConfig", "Logging")]
    [InlineData("Options", "Options")]  // Edge case: just "Options" stays as-is
    [InlineData("Settings", "Settings")]  // Edge case: just "Settings" stays as-is
    [InlineData("Config", "Config")]  // Edge case: just "Config" stays as-is
    public void InferSectionName_FromClassName_StripsSuffixes(string className, string expected)
    {
        var result = OptionsNamingHelper.InferSectionName(className);
        
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("DatabaseOptions", "Options")]
    [InlineData("CacheSettings", "Settings")]
    [InlineData("RedisConfig", "Config")]
    [InlineData("FeatureFlags", null)]  // No suffix match
    [InlineData("Stripe", null)]
    public void GetMatchedSuffix_ReturnsCorrectSuffix(string className, string? expectedSuffix)
    {
        var result = OptionsNamingHelper.GetMatchedSuffix(className);
        
        Assert.Equal(expectedSuffix, result);
    }
}
