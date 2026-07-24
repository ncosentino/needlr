using System;

namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// A positional primary-constructor parameter forwarded by a generated record
/// constructor overload.
/// </summary>
internal readonly struct RecordConstructorPrimaryParameter :
    IEquatable<RecordConstructorPrimaryParameter>
{
    public RecordConstructorPrimaryParameter(
        string name,
        string escapedName,
        string typeName,
        string declarationModifier,
        string argumentModifier,
        string documentation)
    {
        Name = name;
        EscapedName = escapedName;
        TypeName = typeName;
        DeclarationModifier = declarationModifier;
        ArgumentModifier = argumentModifier;
        Documentation = documentation;
    }

    public string Name { get; }

    public string EscapedName { get; }

    public string TypeName { get; }

    public string DeclarationModifier { get; }

    public string ArgumentModifier { get; }

    public string Documentation { get; }

    public bool Equals(RecordConstructorPrimaryParameter other)
    {
        return string.Equals(Name, other.Name, StringComparison.Ordinal) &&
            string.Equals(EscapedName, other.EscapedName, StringComparison.Ordinal) &&
            string.Equals(TypeName, other.TypeName, StringComparison.Ordinal) &&
            string.Equals(
                DeclarationModifier,
                other.DeclarationModifier,
                StringComparison.Ordinal) &&
            string.Equals(
                ArgumentModifier,
                other.ArgumentModifier,
                StringComparison.Ordinal) &&
            string.Equals(
                Documentation,
                other.Documentation,
                StringComparison.Ordinal);
    }

    public override bool Equals(object? obj)
    {
        return obj is RecordConstructorPrimaryParameter other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = Name.GetHashCode();
            hash = (hash * 397) ^ EscapedName.GetHashCode();
            hash = (hash * 397) ^ TypeName.GetHashCode();
            hash = (hash * 397) ^ DeclarationModifier.GetHashCode();
            hash = (hash * 397) ^ ArgumentModifier.GetHashCode();
            hash = (hash * 397) ^ Documentation.GetHashCode();
            return hash;
        }
    }

    public static bool operator ==(
        RecordConstructorPrimaryParameter left,
        RecordConstructorPrimaryParameter right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(
        RecordConstructorPrimaryParameter left,
        RecordConstructorPrimaryParameter right)
    {
        return !left.Equals(right);
    }
}
