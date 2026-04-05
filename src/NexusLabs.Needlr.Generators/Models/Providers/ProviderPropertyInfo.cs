namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// Information about a property on a Provider.
/// </summary>
internal readonly struct ProviderPropertyInfo
{
    public ProviderPropertyInfo(
        string propertyName,
        string serviceTypeName,
        ProviderPropertyKind kind)
    {
        PropertyName = propertyName;
        ServiceTypeName = serviceTypeName;
        Kind = kind;
    }

    /// <summary>Property name on the generated provider.</summary>
    public string PropertyName { get; }

    /// <summary>Fully qualified service type name.</summary>
    public string ServiceTypeName { get; }

    /// <summary>How this property should be resolved.</summary>
    public ProviderPropertyKind Kind { get; }
}
