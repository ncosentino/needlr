using System;
using System.Linq;

namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// The complete shared model describing a generated constructor for a single partial
/// class. Consumed by both source emission and Needlr's type-registry constructor
/// discovery so both agree on the effective constructor shape.
/// </summary>
/// <remarks>
/// Implements value equality (rather than the default struct member-by-member
/// reflection-based comparison) — in particular <see cref="Fields"/> is compared
/// element-by-element rather than by array reference — so that the incremental
/// generator pipeline can cache this model across unrelated edits: an edit to one
/// type's fields must not force every other eligible type's model to be considered
/// "changed" just because a new (but equal) model instance was rebuilt for it.
/// </remarks>
internal readonly struct GeneratedConstructorModel : IEquatable<GeneratedConstructorModel>
{
    public GeneratedConstructorModel(
        string containingNamespace,
        string containingTypeName,
        string typeParameterList,
        int arity,
        GeneratedConstructorNullGuardMode nullGuardMode,
        EligibleConstructorField[] fields,
        string? sourceFilePath)
    {
        ContainingNamespace = containingNamespace;
        ContainingTypeName = containingTypeName;
        TypeParameterList = typeParameterList;
        Arity = arity;
        NullGuardMode = nullGuardMode;
        Fields = fields;
        SourceFilePath = sourceFilePath;
    }

    /// <summary>
    /// The containing namespace, or an empty string for the global namespace.
    /// </summary>
    public string ContainingNamespace { get; }

    /// <summary>
    /// The simple containing type name, without namespace or type parameters.
    /// </summary>
    public string ContainingTypeName { get; }

    /// <summary>
    /// The type parameter list syntax (e.g. <c>&lt;T&gt;</c>), or an empty string for
    /// non-generic types.
    /// </summary>
    public string TypeParameterList { get; }

    /// <summary>
    /// The type's generic arity (number of type parameters). Used, alongside
    /// <see cref="ContainingNamespace"/> and <see cref="ContainingTypeName"/>, as a
    /// deterministic hint-name discriminator so that two distinct types sharing the
    /// same namespace and name but different arity (e.g. <c>Foo</c> and
    /// <c>Foo&lt;T&gt;</c>) never collide on the same generated file name — the same
    /// discriminator the CLR itself uses to distinguish same-named generic arities
    /// within a namespace.
    /// </summary>
    public int Arity { get; }

    /// <summary>
    /// The class-level null-guard mode in effect for this constructor.
    /// </summary>
    public GeneratedConstructorNullGuardMode NullGuardMode { get; }

    /// <summary>
    /// Eligible fields, in deterministic declaration order, that become constructor
    /// parameters.
    /// </summary>
    public EligibleConstructorField[] Fields { get; }

    /// <summary>
    /// The source file path of the containing type declaration, used for breadcrumbs.
    /// </summary>
    public string? SourceFilePath { get; }

    public bool Equals(GeneratedConstructorModel other)
    {
        return string.Equals(ContainingNamespace, other.ContainingNamespace, StringComparison.Ordinal) &&
            string.Equals(ContainingTypeName, other.ContainingTypeName, StringComparison.Ordinal) &&
            string.Equals(TypeParameterList, other.TypeParameterList, StringComparison.Ordinal) &&
            Arity == other.Arity &&
            NullGuardMode == other.NullGuardMode &&
            string.Equals(SourceFilePath, other.SourceFilePath, StringComparison.Ordinal) &&
            FieldsEqual(Fields, other.Fields);
    }

    public override bool Equals(object? obj) => obj is GeneratedConstructorModel other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = ContainingNamespace.GetHashCode();
            hash = (hash * 397) ^ ContainingTypeName.GetHashCode();
            hash = (hash * 397) ^ TypeParameterList.GetHashCode();
            hash = (hash * 397) ^ Arity;
            hash = (hash * 397) ^ (int)NullGuardMode;
            hash = (hash * 397) ^ (SourceFilePath?.GetHashCode() ?? 0);

            foreach (var field in Fields)
            {
                hash = (hash * 397) ^ field.GetHashCode();
            }

            return hash;
        }
    }

    public static bool operator ==(GeneratedConstructorModel left, GeneratedConstructorModel right) => left.Equals(right);

    public static bool operator !=(GeneratedConstructorModel left, GeneratedConstructorModel right) => !left.Equals(right);

    private static bool FieldsEqual(EligibleConstructorField[] left, EligibleConstructorField[] right)
    {
        if (ReferenceEquals(left, right))
            return true;

        if (left.Length != right.Length)
            return false;

        return left.SequenceEqual(right);
    }
}
