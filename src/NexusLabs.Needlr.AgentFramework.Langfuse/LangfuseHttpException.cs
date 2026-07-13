using System.Net;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Carries structured HTTP failure details used by Langfuse retry and reconciliation logic.
/// </summary>
internal sealed class LangfuseHttpException : LangfuseException
{
    public LangfuseHttpException(
        string message,
        HttpStatusCode? statusCode,
        TimeSpan? retryAfter,
        bool isTransient,
        bool isTimeout)
        : base(message)
    {
        StatusCode = statusCode;
        RetryAfter = retryAfter;
        IsTransient = isTransient;
        IsTimeout = isTimeout;
    }

    public LangfuseHttpException(
        string message,
        HttpStatusCode? statusCode,
        TimeSpan? retryAfter,
        bool isTransient,
        bool isTimeout,
        Exception innerException)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        RetryAfter = retryAfter;
        IsTransient = isTransient;
        IsTimeout = isTimeout;
    }

    public HttpStatusCode? StatusCode { get; }

    public TimeSpan? RetryAfter { get; }

    public bool IsTransient { get; }

    public bool IsTimeout { get; }
}
