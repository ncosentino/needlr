using System.Collections.Generic;

namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// Information about a positional record's primary constructor parameters.
/// </summary>
internal readonly struct PositionalRecordInfo
{
    public PositionalRecordInfo(
        string shortTypeName,
        string containingNamespace,
        bool isPartial,
        IReadOnlyList<PositionalRecordParameter> parameters)
    {
        ShortTypeName = shortTypeName;
        ContainingNamespace = containingNamespace;
        IsPartial = isPartial;
        Parameters = parameters;
    }

    /// <summary>The simple type name (without namespace).</summary>
    public string ShortTypeName { get; }

    /// <summary>The containing namespace.</summary>
    public string ContainingNamespace { get; }

    /// <summary>Whether the record is declared as partial.</summary>
    public bool IsPartial { get; }

    /// <summary>The primary constructor parameters.</summary>
    public IReadOnlyList<PositionalRecordParameter> Parameters { get; }
}
