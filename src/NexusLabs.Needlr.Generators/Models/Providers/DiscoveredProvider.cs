using System.Collections.Generic;

namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// Information about a discovered Provider (from [Provider] attribute).
/// </summary>
internal readonly struct DiscoveredProvider
{
    public DiscoveredProvider(
        string typeName,
        string assemblyName,
        bool isInterface,
        bool isPartial,
        IReadOnlyList<ProviderPropertyInfo> properties,
        string? sourceFilePath = null)
    {
        TypeName = typeName;
        AssemblyName = assemblyName;
        IsInterface = isInterface;
        IsPartial = isPartial;
        Properties = properties;
        SourceFilePath = sourceFilePath;
    }

    /// <summary>Fully qualified type name of the interface or class.</summary>
    public string TypeName { get; }

    public string AssemblyName { get; }

    /// <summary>True if the [Provider] attribute is on an interface.</summary>
    public bool IsInterface { get; }

    /// <summary>True if the type is a partial class (required for shorthand mode).</summary>
    public bool IsPartial { get; }

    /// <summary>Properties to generate on the provider.</summary>
    public IReadOnlyList<ProviderPropertyInfo> Properties { get; }

    public string? SourceFilePath { get; }

    /// <summary>Gets simple type name without namespace (e.g., "IOrderProvider" from "global::TestApp.IOrderProvider").</summary>
    public string SimpleTypeName
    {
        get
        {
            var name = TypeName;
            var lastDot = name.LastIndexOf('.');
            return lastDot >= 0 ? name.Substring(lastDot + 1) : name;
        }
    }

    /// <summary>Gets the implementation class name (removes leading "I" from interface name if present).</summary>
    public string ImplementationTypeName
    {
        get
        {
            var simple = SimpleTypeName;
            if (IsInterface && simple.StartsWith("I") && simple.Length > 1 && char.IsUpper(simple[1]))
            {
                return simple.Substring(1);
            }
            return simple;
        }
    }

    /// <summary>Gets the interface name (adds leading "I" to class name if needed).</summary>
    public string InterfaceTypeName
    {
        get
        {
            var simple = SimpleTypeName;
            // For non-interfaces, add "I" prefix unless the name already follows interface naming convention (IXxx)
            if (!IsInterface)
            {
                // Only treat as already having interface prefix if it starts with "I" followed by uppercase letter
                if (simple.Length > 1 && simple[0] == 'I' && char.IsUpper(simple[1]))
                {
                    return simple;
                }
                return "I" + simple;
            }
            return simple;
        }
    }
}
