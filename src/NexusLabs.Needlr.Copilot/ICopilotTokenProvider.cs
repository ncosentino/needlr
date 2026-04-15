namespace NexusLabs.Needlr.Copilot;

/// <summary>
/// Provides short-lived Copilot API tokens obtained via GitHub OAuth token exchange.
/// </summary>
public interface ICopilotTokenProvider
{
    /// <summary>
    /// Gets a valid Copilot API token, refreshing if expired.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A valid Copilot API bearer token.</returns>
    Task<string> GetTokenAsync(CancellationToken cancellationToken = default);
}
