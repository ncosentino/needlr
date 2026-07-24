using System;
using System.Linq;

namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// A normalized constructor guard to emit for a generated-constructor parameter,
/// either a built-in kind or a resolved custom guard type and method call.
/// </summary>
/// <remarks>
/// Implements value equality so generated-constructor and record-overload models that
/// contain arrays of guards remain cacheable across incremental generator runs.
/// Positional literals forwarded from a parameterized alias are compared
/// element-by-element rather than by array reference.
/// </remarks>
internal readonly struct ConstructorGuardModel : IEquatable<ConstructorGuardModel>
{
    public ConstructorGuardModel(
        GeneratedConstructorGuardKind kind,
        string? customGuardTypeName,
        string? customGuardMethodName,
        string[]? forwardedArgumentLiterals)
    {
        Kind = kind;
        CustomGuardTypeName = customGuardTypeName;
        CustomGuardMethodName = customGuardMethodName;
        ForwardedArgumentLiterals = forwardedArgumentLiterals ?? Array.Empty<string>();
    }

    /// <summary>
    /// The guard kind. <see cref="GeneratedConstructorGuardKind.Custom"/> indicates a
    /// resolved custom guard type and method call.
    /// </summary>
    public GeneratedConstructorGuardKind Kind { get; }

    /// <summary>
    /// The fully qualified custom guard type name, or <see langword="null"/> for a
    /// built-in guard.
    /// </summary>
    public string? CustomGuardTypeName { get; }

    /// <summary>
    /// The resolved custom guard method name, or <see langword="null"/> for a built-in
    /// guard.
    /// </summary>
    public string? CustomGuardMethodName { get; }

    /// <summary>
    /// Rendered C# literals forwarded from a parameterized alias attribute usage, in
    /// declared order.
    /// </summary>
    public string[] ForwardedArgumentLiterals { get; }

    public bool Equals(ConstructorGuardModel other)
    {
        return Kind == other.Kind &&
            string.Equals(CustomGuardTypeName, other.CustomGuardTypeName, StringComparison.Ordinal) &&
            string.Equals(CustomGuardMethodName, other.CustomGuardMethodName, StringComparison.Ordinal) &&
            ForwardedArgumentLiteralsEqual(ForwardedArgumentLiterals, other.ForwardedArgumentLiterals);
    }

    public override bool Equals(object? obj) => obj is ConstructorGuardModel other && Equals(other);

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

    public static bool operator ==(ConstructorGuardModel left, ConstructorGuardModel right) => left.Equals(right);

    public static bool operator !=(ConstructorGuardModel left, ConstructorGuardModel right) => !left.Equals(right);

    private static bool ForwardedArgumentLiteralsEqual(string[] left, string[] right)
    {
        if (ReferenceEquals(left, right))
            return true;

        if (left.Length != right.Length)
            return false;

        return left.SequenceEqual(right, StringComparer.Ordinal);
    }
}
