using System;

namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// A single resolved constructor guard to emit for a field, either a built-in kind or
/// a resolved custom guard type/method call.
/// </summary>
/// <remarks>
/// Implements value equality (rather than the default struct member-by-member
/// reflection-based comparison) so that <see cref="EligibleConstructorField"/> and
/// <see cref="GeneratedConstructorModel"/> — which hold arrays of these guards — remain
/// cacheable across incremental generator runs: unrelated edits must not force this
/// type's generated constructor to be recomputed just because array-of-struct equality
/// was never precisely defined.
/// </remarks>
internal readonly struct ConstructorFieldGuard : IEquatable<ConstructorFieldGuard>
{
    public ConstructorFieldGuard(
        GeneratedConstructorGuardKind kind,
        string? customGuardTypeName = null,
        string? customGuardMethodName = null)
    {
        Kind = kind;
        CustomGuardTypeName = customGuardTypeName;
        CustomGuardMethodName = customGuardMethodName;
    }

    /// <summary>
    /// The guard kind. <see cref="GeneratedConstructorGuardKind.Custom"/> indicates a
    /// resolved custom guard type/method call described by
    /// <see cref="CustomGuardTypeName"/> and <see cref="CustomGuardMethodName"/>.
    /// </summary>
    public GeneratedConstructorGuardKind Kind { get; }

    /// <summary>
    /// The fully qualified name of the custom guard type, or <see langword="null"/>
    /// for built-in guard kinds.
    /// </summary>
    public string? CustomGuardTypeName { get; }

    /// <summary>
    /// The resolved static validation method name on <see cref="CustomGuardTypeName"/>,
    /// or <see langword="null"/> for built-in guard kinds.
    /// </summary>
    public string? CustomGuardMethodName { get; }

    public bool Equals(ConstructorFieldGuard other)
    {
        return Kind == other.Kind &&
            string.Equals(CustomGuardTypeName, other.CustomGuardTypeName, StringComparison.Ordinal) &&
            string.Equals(CustomGuardMethodName, other.CustomGuardMethodName, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj) => obj is ConstructorFieldGuard other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = (int)Kind;
            hash = (hash * 397) ^ (CustomGuardTypeName?.GetHashCode() ?? 0);
            hash = (hash * 397) ^ (CustomGuardMethodName?.GetHashCode() ?? 0);
            return hash;
        }
    }

    public static bool operator ==(ConstructorFieldGuard left, ConstructorFieldGuard right) => left.Equals(right);

    public static bool operator !=(ConstructorFieldGuard left, ConstructorFieldGuard right) => !left.Equals(right);
}
