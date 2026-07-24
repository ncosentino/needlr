using System;

namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// Stable scalar context combined with each record-overload model during emission.
/// </summary>
internal readonly struct RecordConstructorOverloadEmitContext :
    IEquatable<RecordConstructorOverloadEmitContext>
{
    public RecordConstructorOverloadEmitContext(
        string assemblyName,
        BreadcrumbLevel breadcrumbLevel)
    {
        AssemblyName = assemblyName;
        BreadcrumbLevel = breadcrumbLevel;
    }

    public string AssemblyName { get; }

    public BreadcrumbLevel BreadcrumbLevel { get; }

    public bool Equals(RecordConstructorOverloadEmitContext other)
    {
        return string.Equals(
                AssemblyName,
                other.AssemblyName,
                StringComparison.Ordinal) &&
            BreadcrumbLevel == other.BreadcrumbLevel;
    }

    public override bool Equals(object? obj)
    {
        return obj is RecordConstructorOverloadEmitContext other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return (AssemblyName.GetHashCode() * 397) ^ (int)BreadcrumbLevel;
        }
    }

    public static bool operator ==(
        RecordConstructorOverloadEmitContext left,
        RecordConstructorOverloadEmitContext right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(
        RecordConstructorOverloadEmitContext left,
        RecordConstructorOverloadEmitContext right)
    {
        return !left.Equals(right);
    }
}
