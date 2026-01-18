namespace NexusLabs.Needlr.Injection;

/// <summary>
/// Provides context information when a reflection-based component is used as a fallback
/// because no source-generated component is available.
/// </summary>
[DoNotAutoRegister]
public sealed class ReflectionFallbackContext
{
    /// <summary>
    /// Gets the name of the component that triggered the reflection fallback.
    /// </summary>
    public required string ComponentName { get; init; }

    /// <summary>
    /// Gets a description of why the fallback occurred.
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>
    /// Gets the type of the reflection-based component being used.
    /// </summary>
    public required Type ReflectionComponentType { get; init; }

    /// <summary>
    /// Gets the type of the source-generated component that would have been used if available.
    /// </summary>
    public required Type GeneratedComponentType { get; init; }
}
