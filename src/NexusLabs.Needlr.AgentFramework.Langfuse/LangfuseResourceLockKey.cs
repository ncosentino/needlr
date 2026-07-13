using System.Security.Cryptography;
using System.Text;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Creates stable opaque keys for project-scoped resource locks.
/// </summary>
internal static class LangfuseResourceLockKey
{
    public static string Create(
        string scope,
        string resourceType,
        string resourceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceType);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);

        var input = $"{scope.Length}:{scope}{resourceType.Length}:{resourceType}{resourceName.Length}:{resourceName}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return $"needlr-langfuse:{Convert.ToHexStringLower(hash)}";
    }
}
