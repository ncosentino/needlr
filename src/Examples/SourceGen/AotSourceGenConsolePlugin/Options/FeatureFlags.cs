using NexusLabs.Needlr.Generators;

namespace AotSourceGenConsolePlugin.Options;

/// <summary>
/// Feature flags configuration.
/// The source generator uses the full class name "FeatureFlags" as section name (no suffix to strip).
/// </summary>
[Options]
public class FeatureFlags
{
    /// <summary>Gets or sets whether the new UI is enabled.</summary>
    public bool NewUiEnabled { get; set; } = false;

    /// <summary>Gets or sets whether async processing is enabled.</summary>
    public bool AsyncProcessingEnabled { get; set; } = true;

    /// <summary>Gets or sets the beta feature rollout percentage.</summary>
    public int BetaRolloutPercent { get; set; } = 0;
}
