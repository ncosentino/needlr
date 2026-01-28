namespace NexusLabs.Needlr.Generators.Helpers;

/// <summary>
/// Helper methods for inferring configuration section names from options class names.
/// </summary>
public static class OptionsNamingHelper
{
    /// <summary>
    /// Common suffixes that are stripped when inferring section names.
    /// </summary>
    private static readonly string[] Suffixes = ["Options", "Settings", "Config"];

    /// <summary>
    /// Infers the configuration section name from a class name.
    /// Strips common suffixes (Options, Settings, Config) if present and the result is non-empty.
    /// </summary>
    /// <param name="className">The options class name.</param>
    /// <returns>The inferred section name.</returns>
    /// <example>
    /// <code>
    /// InferSectionName("DatabaseOptions") // returns "Database"
    /// InferSectionName("CacheSettings")   // returns "Cache"
    /// InferSectionName("RedisConfig")     // returns "Redis"
    /// InferSectionName("FeatureFlags")    // returns "FeatureFlags" (no suffix match)
    /// InferSectionName("Options")         // returns "Options" (would be empty after stripping)
    /// </code>
    /// </example>
    public static string InferSectionName(string className)
    {
        foreach (var suffix in Suffixes)
        {
            if (className.EndsWith(suffix, System.StringComparison.Ordinal) && 
                className.Length > suffix.Length)
            {
                return className.Substring(0, className.Length - suffix.Length);
            }
        }
        return className;
    }

    /// <summary>
    /// Gets the suffix that was matched and would be stripped from the class name, if any.
    /// </summary>
    /// <param name="className">The options class name.</param>
    /// <returns>The matched suffix, or null if no suffix matches.</returns>
    public static string? GetMatchedSuffix(string className)
    {
        foreach (var suffix in Suffixes)
        {
            if (className.EndsWith(suffix, System.StringComparison.Ordinal) && 
                className.Length > suffix.Length)
            {
                return suffix;
            }
        }
        return null;
    }
}
