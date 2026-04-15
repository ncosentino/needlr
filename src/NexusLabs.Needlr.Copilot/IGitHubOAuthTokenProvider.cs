namespace NexusLabs.Needlr.Copilot;

/// <summary>
/// Provides the raw GitHub OAuth token used for GitHub API calls (token exchange)
/// and MCP server authentication. This is the foundational credential — the
/// Copilot API token is derived from it via <see cref="ICopilotTokenProvider"/>.
/// </summary>
public interface IGitHubOAuthTokenProvider
{
    /// <summary>
    /// Gets the raw GitHub OAuth token (e.g., <c>gho_xxx</c> from apps.json or <c>GH_TOKEN</c>).
    /// </summary>
    /// <returns>The raw OAuth token string.</returns>
    string GetOAuthToken();
}
