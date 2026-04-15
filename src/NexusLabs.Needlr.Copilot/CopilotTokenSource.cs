namespace NexusLabs.Needlr.Copilot;

/// <summary>
/// Controls where the GitHub OAuth token is sourced from.
/// </summary>
public enum CopilotTokenSource
{
    /// <summary>
    /// Try the Copilot CLI's local <c>apps.json</c> first, then environment variables.
    /// </summary>
    Auto,

    /// <summary>
    /// Read from <c>%LOCALAPPDATA%\github-copilot\apps.json</c> (or platform equivalent).
    /// </summary>
    AppsJson,

    /// <summary>
    /// Read from <c>GH_TOKEN</c> or <c>GITHUB_TOKEN</c> environment variable.
    /// </summary>
    EnvironmentVariable,
}
