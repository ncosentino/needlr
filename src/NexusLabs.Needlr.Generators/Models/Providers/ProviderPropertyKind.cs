namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// Indicates how a provider property should be resolved.
/// </summary>
internal enum ProviderPropertyKind
{
    /// <summary>Required service - uses GetRequiredService&lt;T&gt;().</summary>
    Required,

    /// <summary>Optional service - uses GetService&lt;T&gt;() and is nullable.</summary>
    Optional,

    /// <summary>Collection of services - uses GetServices&lt;T&gt;().</summary>
    Collection,

    /// <summary>Factory for creating new instances.</summary>
    Factory
}
