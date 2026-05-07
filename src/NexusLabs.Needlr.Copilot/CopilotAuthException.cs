namespace NexusLabs.Needlr.Copilot;

/// <summary>
/// Thrown when a Copilot request fails because the caller is not
/// authenticated. This covers two distinct cases:
/// </summary>
/// <remarks>
/// <para>
/// 1. <b>No token configured locally.</b>
/// <see cref="GitHubOAuthTokenProvider"/> could not resolve a GitHub OAuth
/// token from any configured source (explicit option, apps.json, or
/// environment variables). The HTTP request is never sent.
/// </para>
/// <para>
/// 2. <b>Token rejected by the server.</b>
/// <see cref="CopilotMcpToolClient"/> received an HTTP <c>401 Unauthorized</c>
/// or <c>403 Forbidden</c> response from the Copilot MCP endpoint, indicating
/// the supplied token is invalid, expired, or lacks the required scopes.
/// </para>
/// <para>
/// Callers should catch this exception to implement fallback behavior such
/// as switching to an alternative search provider or surfacing a
/// re-authentication prompt to the user. This is the auth analog of
/// <see cref="CopilotRateLimitException"/> and exists so consumers do not
/// have to string-match free-text error messages from the upstream server.
/// </para>
/// </remarks>
public sealed class CopilotAuthException : Exception
{
    /// <summary>
    /// Creates a new <see cref="CopilotAuthException"/> with the given
    /// human-readable message.
    /// </summary>
    /// <param name="message">A description of the auth failure.</param>
    public CopilotAuthException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Creates a new <see cref="CopilotAuthException"/> wrapping the given
    /// inner exception.
    /// </summary>
    /// <param name="message">A description of the auth failure.</param>
    /// <param name="innerException">The underlying exception that caused this failure.</param>
    public CopilotAuthException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
