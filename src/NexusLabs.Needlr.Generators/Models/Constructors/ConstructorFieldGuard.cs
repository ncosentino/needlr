using System;
using System.Linq;

namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// A single resolved constructor guard to emit for a field, either a built-in kind or
/// a resolved custom guard type/method call, optionally carrying positional literal
/// arguments forwarded from a parameterized alias attribute usage (e.g.
/// <c>[MinCount(3)]</c>).
/// </summary>
/// <remarks>
/// Implements value equality (rather than the default struct member-by-member
/// reflection-based comparison) so that <see cref="EligibleConstructorField"/> and
/// <see cref="GeneratedConstructorModel"/> — which hold arrays of these guards — remain
/// cacheable across incremental generator runs: unrelated edits must not force this
/// type's generated constructor to be recomputed just because array-of-struct equality
/// was never precisely defined. <see cref="ForwardedArgumentLiterals"/> is compared
/// element-by-element for the same reason, so editing only the literal argument of an
/// alias attribute usage (e.g. changing <c>[MinCount(3)]</c> to <c>[MinCount(5)]</c>)
/// is correctly observed as a change rather than silently reusing a stale cached model.
/// </remarks>
internal readonly struct ConstructorFieldGuard : IEquatable<ConstructorFieldGuard>
{
    public ConstructorFieldGuard(
        GeneratedConstructorGuardKind kind,
        string? customGuardTypeName = null,
        string? customGuardMethodName = null,
        string[]? forwardedArgumentLiterals = null)
    {
        Kind = kind;
        CustomGuardTypeName = customGuardTypeName;
        CustomGuardMethodName = customGuardMethodName;
        ForwardedArgumentLiterals = forwardedArgumentLiterals ?? Array.Empty<string>();
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

    /// <summary>
    /// Already-rendered C# literal/expression source text for each positional
    /// constructor argument of a parameterized alias attribute usage, in declared
    /// order -- e.g. <c>["3"]</c> for <c>[MinCount(3)]</c>. Always empty (never
    /// <see langword="null"/>) for a built-in guard kind or a direct
    /// <c>[ConstructorGuard(typeof(...))]</c> usage, both of which forward zero
    /// arguments. Rendering happens at discovery time via
    /// <see cref="NexusLabs.Needlr.Generators.TypedConstantRenderer"/> so that this
    /// model never carries a Roslyn symbol or <c>TypedConstant</c> forward.
    /// </summary>
    public string[] ForwardedArgumentLiterals { get; }

    public bool Equals(ConstructorFieldGuard other)
    {
        return Kind == other.Kind &&
            string.Equals(CustomGuardTypeName, other.CustomGuardTypeName, StringComparison.Ordinal) &&
            string.Equals(CustomGuardMethodName, other.CustomGuardMethodName, StringComparison.Ordinal) &&
            ForwardedArgumentLiteralsEqual(ForwardedArgumentLiterals, other.ForwardedArgumentLiterals);
    }

    public override bool Equals(object? obj) => obj is ConstructorFieldGuard other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = (int)Kind;
            hash = (hash * 397) ^ (CustomGuardTypeName?.GetHashCode() ?? 0);
            hash = (hash * 397) ^ (CustomGuardMethodName?.GetHashCode() ?? 0);

            foreach (var literal in ForwardedArgumentLiterals)
            {
                hash = (hash * 397) ^ literal.GetHashCode();
            }

            return hash;
        }
    }

    public static bool operator ==(ConstructorFieldGuard left, ConstructorFieldGuard right) => left.Equals(right);

    public static bool operator !=(ConstructorFieldGuard left, ConstructorFieldGuard right) => !left.Equals(right);

    private static bool ForwardedArgumentLiteralsEqual(string[] left, string[] right)
    {
        if (ReferenceEquals(left, right))
            return true;

        if (left.Length != right.Length)
            return false;

        return left.SequenceEqual(right, StringComparer.Ordinal);
    }
}
