using System.Globalization;
using System.Text.RegularExpressions;

namespace NexusLabs.Needlr.Copilot;

/// <summary>
/// Thrown when a Copilot web search request is rejected due to rate limiting.
/// This can occur either via an HTTP 429 response after retry exhaustion or
/// when the MCP tool returns a rate-limit error message as its content.
/// </summary>
/// <remarks>
/// Callers should catch this exception to implement fallback behavior
/// (e.g., trying an alternative search provider or waiting before retrying).
/// <see cref="RetryAfter"/> provides a hint for how long to wait, when
/// available from the response.
/// </remarks>
public sealed partial class CopilotRateLimitException : Exception
{
    /// <summary>
    /// Creates a new <see cref="CopilotRateLimitException"/> with the given
    /// message, optional retry delay, and optional inner exception.
    /// </summary>
    /// <param name="message">A description of the rate-limit condition.</param>
    /// <param name="retryAfter">
    /// How long the caller should wait before retrying, if known from the
    /// HTTP <c>Retry-After</c> header or the error message text.
    /// </param>
    /// <param name="innerException">The exception that caused this failure, if any.</param>
    public CopilotRateLimitException(
        string message,
        TimeSpan? retryAfter = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        RetryAfter = retryAfter;
    }

    /// <summary>
    /// The suggested wait duration before retrying, parsed from the HTTP
    /// <c>Retry-After</c> header or the error message text. <c>null</c> when
    /// no retry hint was provided.
    /// </summary>
    public TimeSpan? RetryAfter { get; }

    /// <summary>
    /// Attempts to parse a "Try again in N seconds" hint from the given text.
    /// </summary>
    internal static TimeSpan? ParseRetryAfterFromText(string text)
    {
        var match = RetryAfterPattern().Match(text);
        if (match.Success &&
            int.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, out var seconds))
        {
            return TimeSpan.FromSeconds(seconds);
        }

        return null;
    }

    [GeneratedRegex(@"Try again in (\d+) seconds?", RegexOptions.IgnoreCase)]
    private static partial Regex RetryAfterPattern();
}
