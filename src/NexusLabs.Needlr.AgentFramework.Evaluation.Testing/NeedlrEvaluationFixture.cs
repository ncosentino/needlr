using Microsoft.Extensions.AI;
using NexusLabs.Needlr.Copilot;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Testing;

/// <summary>
/// xUnit fixture that discovers an <see cref="IChatClient"/> judge for evaluation tests.
///
/// Discovery order:
///   1. An explicitly-provided <see cref="IChatClient"/> supplied by a derived fixture.
///   2. The GitHub Copilot provider, when a GitHub OAuth token is resolvable from
///      the Copilot CLI apps.json or <c>GH_TOKEN</c> / <c>GITHUB_TOKEN</c>.
///
/// When no judge can be discovered, <see cref="IsAvailable"/> is <see langword="false"/>
/// and <see cref="UnavailableReason"/> carries a human-readable explanation.
/// </summary>
/// <remarks>
/// Azure OpenAI is intentionally NOT discovered. Evaluation tests that fall back to
/// Azure would burn paid tokens on every CI run; the Copilot provider is the only
/// cost-safe default. Supply a custom <see cref="IChatClient"/> via the protected
/// constructor if an alternative provider is required.
/// </remarks>
public class NeedlrEvaluationFixture : IDisposable
{
    private readonly IChatClient? _judge;
    private readonly bool _ownsJudge;
    private bool _disposed;

    /// <summary>
    /// Creates a fixture that probes the Copilot provider for a usable judge.
    /// </summary>
    public NeedlrEvaluationFixture()
    {
        (_judge, UnavailableReason) = TryCreateCopilotJudge();
        _ownsJudge = _judge is not null;
    }

    /// <summary>
    /// Creates a fixture with an explicit judge. Disposal of <paramref name="judge"/>
    /// is the caller's responsibility.
    /// </summary>
    protected NeedlrEvaluationFixture(IChatClient judge)
    {
        _judge = judge ?? throw new ArgumentNullException(nameof(judge));
        _ownsJudge = false;
        UnavailableReason = null;
    }

    /// <summary>
    /// The discovered chat client judge. Throws when <see cref="IsAvailable"/> is
    /// <see langword="false"/>; prefer guarding access with <see cref="IsAvailable"/>
    /// or relying on <c>[NeedlrEvaluationFact]</c> / <c>[NeedlrEvaluationTheory]</c>
    /// to skip tests automatically.
    /// </summary>
    public IChatClient Judge =>
        _judge ?? throw new InvalidOperationException(
            $"No evaluation judge is available. {UnavailableReason}");

    /// <summary>
    /// Whether a judge was successfully discovered.
    /// </summary>
    public bool IsAvailable => _judge is not null;

    /// <summary>
    /// A human-readable explanation of why no judge was discovered, or
    /// <see langword="null"/> when <see cref="IsAvailable"/> is <see langword="true"/>.
    /// </summary>
    public string? UnavailableReason { get; }

    private static (IChatClient?, string?) TryCreateCopilotJudge()
    {
        var options = new CopilotChatClientOptions();

        try
        {
            var provider = new GitHubOAuthTokenProvider(options);
            _ = provider.GetOAuthToken();
        }
        catch (InvalidOperationException ex)
        {
            return (null, ex.Message);
        }

        return (new CopilotChatClient(options), null);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes resources owned by this fixture.
    /// </summary>
    /// <param name="disposing">Whether the call is from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing && _ownsJudge && _judge is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _disposed = true;
    }
}
