using NexusLabs.Needlr.Generators;

namespace AotSourceGenConsolePlugin.Options;

/// <summary>
/// Cache configuration options.
/// The source generator automatically infers section name "Cache" from the class name.
/// </summary>
[Options]
public class CacheSettings
{
    /// <summary>Gets or sets whether caching is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the default expiration in minutes.</summary>
    public int ExpirationMinutes { get; set; } = 30;

    /// <summary>Gets or sets the maximum cache size in MB.</summary>
    public int MaxSizeMb { get; set; } = 100;
}
