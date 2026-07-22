using System;
using System.Linq;

namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// A single field eligible to become a generated constructor parameter.
/// </summary>
/// <remarks>
/// Implements value equality (rather than the default struct member-by-member
/// reflection-based comparison) — in particular <see cref="ExplicitGuards"/> is
/// compared element-by-element rather than by array reference — so that
/// <see cref="GeneratedConstructorModel"/> stays cacheable across incremental
/// generator runs: two separately-built field lists with the same effective content
/// must compare equal even though they are different array instances.
/// </remarks>
internal readonly struct EligibleConstructorField : IEquatable<EligibleConstructorField>
{
    public EligibleConstructorField(
        string fieldName,
        string parameterName,
        string parameterTypeName,
        bool isNonNullableReferenceType,
        ConstructorFieldGuard[] explicitGuards)
    {
        FieldName = fieldName;
        ParameterName = parameterName;
        ParameterTypeName = parameterTypeName;
        IsNonNullableReferenceType = isNonNullableReferenceType;
        ExplicitGuards = explicitGuards;
    }

    /// <summary>
    /// The original field name, e.g. <c>_repository</c>.
    /// </summary>
    public string FieldName { get; }

    /// <summary>
    /// The normalized constructor parameter name, e.g. <c>repository</c>.
    /// </summary>
    public string ParameterName { get; }

    /// <summary>
    /// The fully qualified parameter type name, preserving nullable-reference and
    /// generic type syntax.
    /// </summary>
    public string ParameterTypeName { get; }

    /// <summary>
    /// True when the field's type is a reference type with no nullable annotation,
    /// making it eligible for the class-level automatic null-guard default.
    /// </summary>
    public bool IsNonNullableReferenceType { get; }

    /// <summary>
    /// Explicit guards requested for this field, in source declaration order.
    /// </summary>
    public ConstructorFieldGuard[] ExplicitGuards { get; }

    public bool Equals(EligibleConstructorField other)
    {
        return string.Equals(FieldName, other.FieldName, StringComparison.Ordinal) &&
            string.Equals(ParameterName, other.ParameterName, StringComparison.Ordinal) &&
            string.Equals(ParameterTypeName, other.ParameterTypeName, StringComparison.Ordinal) &&
            IsNonNullableReferenceType == other.IsNonNullableReferenceType &&
            GuardsEqual(ExplicitGuards, other.ExplicitGuards);
    }

    public override bool Equals(object? obj) => obj is EligibleConstructorField other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = FieldName.GetHashCode();
            hash = (hash * 397) ^ ParameterName.GetHashCode();
            hash = (hash * 397) ^ ParameterTypeName.GetHashCode();
            hash = (hash * 397) ^ IsNonNullableReferenceType.GetHashCode();

            foreach (var guard in ExplicitGuards)
            {
                hash = (hash * 397) ^ guard.GetHashCode();
            }

            return hash;
        }
    }

    public static bool operator ==(EligibleConstructorField left, EligibleConstructorField right) => left.Equals(right);

    public static bool operator !=(EligibleConstructorField left, EligibleConstructorField right) => !left.Equals(right);

    private static bool GuardsEqual(ConstructorFieldGuard[] left, ConstructorFieldGuard[] right)
    {
        if (ReferenceEquals(left, right))
            return true;

        if (left.Length != right.Length)
            return false;

        return left.SequenceEqual(right);
    }
}
