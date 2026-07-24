using System;
using System.Linq;

namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// The complete equatable model for one generated positional-record constructor
/// overload.
/// </summary>
internal readonly struct RecordConstructorOverloadModel :
    IEquatable<RecordConstructorOverloadModel>
{
    public RecordConstructorOverloadModel(
        string containingNamespace,
        string containingTypeName,
        string escapedContainingTypeName,
        string typeParameterList,
        int arity,
        RecordConstructorPrimaryParameter[] primaryParameters,
        RecordConstructorPropertyParameter[] propertyParameters,
        string? sourceFilePath)
    {
        ContainingNamespace = containingNamespace;
        ContainingTypeName = containingTypeName;
        EscapedContainingTypeName = escapedContainingTypeName;
        TypeParameterList = typeParameterList;
        Arity = arity;
        PrimaryParameters = primaryParameters;
        PropertyParameters = propertyParameters;
        SourceFilePath = sourceFilePath;
    }

    public string ContainingNamespace { get; }

    public string ContainingTypeName { get; }

    public string EscapedContainingTypeName { get; }

    public string TypeParameterList { get; }

    public int Arity { get; }

    public RecordConstructorPrimaryParameter[] PrimaryParameters { get; }

    public RecordConstructorPropertyParameter[] PropertyParameters { get; }

    public string? SourceFilePath { get; }

    public bool Equals(RecordConstructorOverloadModel other)
    {
        return string.Equals(
                ContainingNamespace,
                other.ContainingNamespace,
                StringComparison.Ordinal) &&
            string.Equals(
                ContainingTypeName,
                other.ContainingTypeName,
                StringComparison.Ordinal) &&
            string.Equals(
                EscapedContainingTypeName,
                other.EscapedContainingTypeName,
                StringComparison.Ordinal) &&
            string.Equals(
                TypeParameterList,
                other.TypeParameterList,
                StringComparison.Ordinal) &&
            Arity == other.Arity &&
            string.Equals(
                SourceFilePath,
                other.SourceFilePath,
                StringComparison.Ordinal) &&
            PrimaryParameters.SequenceEqual(other.PrimaryParameters) &&
            PropertyParameters.SequenceEqual(other.PropertyParameters);
    }

    public override bool Equals(object? obj)
    {
        return obj is RecordConstructorOverloadModel other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = ContainingNamespace.GetHashCode();
            hash = (hash * 397) ^ ContainingTypeName.GetHashCode();
            hash = (hash * 397) ^ EscapedContainingTypeName.GetHashCode();
            hash = (hash * 397) ^ TypeParameterList.GetHashCode();
            hash = (hash * 397) ^ Arity;
            hash = (hash * 397) ^ (SourceFilePath?.GetHashCode() ?? 0);

            foreach (var parameter in PrimaryParameters)
            {
                hash = (hash * 397) ^ parameter.GetHashCode();
            }

            foreach (var parameter in PropertyParameters)
            {
                hash = (hash * 397) ^ parameter.GetHashCode();
            }

            return hash;
        }
    }

    public static bool operator ==(
        RecordConstructorOverloadModel left,
        RecordConstructorOverloadModel right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(
        RecordConstructorOverloadModel left,
        RecordConstructorOverloadModel right)
    {
        return !left.Equals(right);
    }
}
