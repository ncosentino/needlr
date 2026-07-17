using System.Security.Cryptography;
using System.Text;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Represents a stable opaque key for a project-scoped Langfuse resource lock.
/// </summary>
/// <remarks>
/// Distributed <see cref="ILangfuseResourceLockProvider"/> implementations should use
/// <see cref="Value"/> as the backend lock identifier and treat its format as opaque.
/// </remarks>
[DoNotAutoRegister]
public sealed class LangfuseResourceLockKey : IEquatable<LangfuseResourceLockKey>
{
    private LangfuseResourceLockKey(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Gets the stable opaque lock identifier.
    /// </summary>
    public string Value { get; }

    internal static LangfuseResourceLockKey Create(
        string scope,
        string resourceType,
        string resourceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceType);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);

        var input = $"{scope.Length}:{scope}{resourceType.Length}:{resourceType}{resourceName.Length}:{resourceName}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return new LangfuseResourceLockKey(
            $"needlr-langfuse:{Convert.ToHexStringLower(hash)}");
    }

    /// <inheritdoc />
    public bool Equals(LangfuseResourceLockKey? other) =>
        other is not null
        && StringComparer.Ordinal.Equals(Value, other.Value);

    /// <inheritdoc />
    public override bool Equals(object? obj) =>
        obj is LangfuseResourceLockKey other
        && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        StringComparer.Ordinal.GetHashCode(Value);

    /// <inheritdoc />
    public override string ToString() => Value;
}
