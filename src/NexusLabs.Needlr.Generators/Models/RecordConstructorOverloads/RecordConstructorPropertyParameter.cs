using System;
using System.Linq;

namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// A marked record property added to a generated forwarding constructor.
/// </summary>
internal readonly struct RecordConstructorPropertyParameter :
    IEquatable<RecordConstructorPropertyParameter>
{
    public RecordConstructorPropertyParameter(
        string propertyName,
        string escapedPropertyName,
        string typeName,
        string documentation,
        ConstructorGuardModel[] effectiveGuards)
    {
        PropertyName = propertyName;
        EscapedPropertyName = escapedPropertyName;
        TypeName = typeName;
        Documentation = documentation;
        EffectiveGuards = effectiveGuards;
    }

    public string PropertyName { get; }

    public string EscapedPropertyName { get; }

    public string TypeName { get; }

    public string Documentation { get; }

    public ConstructorGuardModel[] EffectiveGuards { get; }

    public bool Equals(RecordConstructorPropertyParameter other)
    {
        return string.Equals(
                PropertyName,
                other.PropertyName,
                StringComparison.Ordinal) &&
            string.Equals(
                EscapedPropertyName,
                other.EscapedPropertyName,
                StringComparison.Ordinal) &&
            string.Equals(TypeName, other.TypeName, StringComparison.Ordinal) &&
            string.Equals(
                Documentation,
                other.Documentation,
                StringComparison.Ordinal) &&
            EffectiveGuards.SequenceEqual(other.EffectiveGuards);
    }

    public override bool Equals(object? obj)
    {
        return obj is RecordConstructorPropertyParameter other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = PropertyName.GetHashCode();
            hash = (hash * 397) ^ EscapedPropertyName.GetHashCode();
            hash = (hash * 397) ^ TypeName.GetHashCode();
            hash = (hash * 397) ^ Documentation.GetHashCode();

            foreach (var guard in EffectiveGuards)
            {
                hash = (hash * 397) ^ guard.GetHashCode();
            }

            return hash;
        }
    }

    public static bool operator ==(
        RecordConstructorPropertyParameter left,
        RecordConstructorPropertyParameter right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(
        RecordConstructorPropertyParameter left,
        RecordConstructorPropertyParameter right)
    {
        return !left.Equals(right);
    }
}
