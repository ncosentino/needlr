using NexusLabs.Needlr.Copilot;
using Xunit;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Testing;

/// <summary>
/// xUnit <c>[Fact]</c> attribute that automatically skips when no evaluation judge
/// is available. A judge is considered available when the GitHub Copilot provider
/// can resolve a GitHub OAuth token from the Copilot CLI apps.json or from
/// <c>GH_TOKEN</c> / <c>GITHUB_TOKEN</c> environment variables.
/// </summary>
/// <remarks>
/// This attribute probes token resolution at test-discovery time. It does not
/// fire an LLM request and does not consume tokens.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class NeedlrEvaluationFactAttribute : FactAttribute
{
    /// <summary>
    /// Creates a fact attribute that skips when no evaluation judge is available.
    /// </summary>
    public NeedlrEvaluationFactAttribute()
    {
        var unavailableReason = JudgeAvailability.GetUnavailableReason();
        if (unavailableReason is not null)
        {
            Skip = unavailableReason;
        }
    }
}

/// <summary>
/// xUnit <c>[Theory]</c> attribute that automatically skips when no evaluation
/// judge is available. See <see cref="NeedlrEvaluationFactAttribute"/> for details.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class NeedlrEvaluationTheoryAttribute : TheoryAttribute
{
    /// <summary>
    /// Creates a theory attribute that skips when no evaluation judge is available.
    /// </summary>
    public NeedlrEvaluationTheoryAttribute()
    {
        var unavailableReason = JudgeAvailability.GetUnavailableReason();
        if (unavailableReason is not null)
        {
            Skip = unavailableReason;
        }
    }
}

internal static class JudgeAvailability
{
    public static string? GetUnavailableReason()
    {
        try
        {
            var provider = new GitHubOAuthTokenProvider(new CopilotChatClientOptions());
            _ = provider.GetOAuthToken();
            return null;
        }
        catch (InvalidOperationException ex)
        {
            return $"No Copilot evaluation judge available: {ex.Message}";
        }
    }
}
