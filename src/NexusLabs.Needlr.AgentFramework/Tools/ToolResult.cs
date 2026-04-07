namespace NexusLabs.Needlr.AgentFramework.Tools;

/// <summary>
/// Non-generic marker interface for inspecting any <see cref="ToolResult{TValue, TError}"/>
/// without knowing its type arguments at compile time.
/// </summary>
public interface IToolResult
{
    /// <summary>Gets a value indicating whether the tool call succeeded.</summary>
    bool IsSuccess { get; }

    /// <summary>
    /// Gets the success value as <see langword="object"/> — sent to the LLM on success.
    /// <see langword="null"/> on failure.
    /// </summary>
    object? BoxedValue { get; }

    /// <summary>
    /// Gets the error value as <see langword="object"/> — sent to the LLM as <c>{ "error": … }</c> on failure.
    /// <see langword="null"/> on success.
    /// </summary>
    object? BoxedError { get; }

    /// <summary>
    /// Gets the original unhandled <see cref="Exception"/>, if any.
    /// <strong>Never sent to the LLM.</strong> Preserved for diagnostics and resilience decisions.
    /// </summary>
    Exception? Exception { get; }

    /// <summary>
    /// Indicates whether the failure is transient and suitable for retry.
    /// <see langword="true"/> = retry, <see langword="false"/> = don't retry,
    /// <see langword="null"/> = let the resilience layer decide via its own heuristics.
    /// </summary>
    bool? IsTransient { get; }
}

/// <summary>
/// Standard return type for <c>[AgentFunction]</c> methods providing two separate channels:
/// one for the LLM (structured JSON, never a raw stack trace) and one for C#
/// (full exception context and retry signal).
/// </summary>
/// <typeparam name="TValue">The success value type. Serialised to JSON for the LLM on success.</typeparam>
/// <typeparam name="TError">
/// The structured error shape. Serialised as <c>{ "error": … }</c> for the LLM on failure.
/// Must be a reference type so <see langword="null"/> can represent the absence of an error.
/// </typeparam>
public readonly struct ToolResult<TValue, TError> : IToolResult
    where TError : class
{
    private readonly bool _isSuccess;
    private readonly TValue? _value;
    private readonly TError? _error;
    private readonly Exception? _exception;
    private readonly bool? _isTransient;

    private ToolResult(
        bool isSuccess,
        TValue? value,
        TError? error,
        Exception? exception,
        bool? isTransient)
    {
        _isSuccess = isSuccess;
        _value = value;
        _error = error;
        _exception = exception;
        _isTransient = isTransient;
    }

    /// <summary>Creates a successful result wrapping <paramref name="value"/>.</summary>
    public static ToolResult<TValue, TError> Ok(TValue value)
        => new(true, value, null, null, null);

    /// <summary>
    /// Creates a failure result with a structured <paramref name="error"/> payload.
    /// </summary>
    /// <param name="error">Structured error sent to the LLM.</param>
    /// <param name="exception">Original exception, preserved for diagnostics (never sent to LLM).</param>
    /// <param name="isTransient">
    /// Whether the failure is transient. <see langword="null"/> lets the resilience layer decide.
    /// </param>
    public static ToolResult<TValue, TError> Fail(
        TError error,
        Exception? exception = null,
        bool? isTransient = null)
        => new(false, default, error, exception, isTransient);

    /// <inheritdoc />
    public bool IsSuccess => _isSuccess;

    /// <summary>Gets the success value. <see langword="default"/> on failure.</summary>
    public TValue? Value => _value;

    /// <summary>Gets the structured error payload. <see langword="null"/> on success.</summary>
    public TError? Error => _error;

    /// <inheritdoc />
    public Exception? Exception => _exception;

    /// <inheritdoc />
    public bool? IsTransient => _isTransient;

    object? IToolResult.BoxedValue => _value;
    object? IToolResult.BoxedError => _error;
}

/// <summary>
/// Opinionated default error shape — sufficient for most tool functions.
/// </summary>
/// <param name="Message">Human-readable error description sent to the LLM.</param>
/// <param name="Suggestion">Optional hint for the LLM on how to recover or try differently.</param>
public sealed record ToolError(string Message, string? Suggestion = null);

/// <summary>
/// Static factory providing shorthand constructors for <see cref="ToolResult{TValue, TError}"/>
/// using the default <see cref="ToolError"/> shape.
/// </summary>
public static class ToolResult
{
    /// <summary>Creates a successful <see cref="ToolResult{TValue, ToolError}"/>.</summary>
    public static ToolResult<TValue, ToolError> Ok<TValue>(TValue value)
        => ToolResult<TValue, ToolError>.Ok(value);

    /// <summary>
    /// Creates a failure <see cref="ToolResult{TValue, ToolError}"/> from a message string.
    /// </summary>
    public static ToolResult<TValue, ToolError> Fail<TValue>(
        string message,
        Exception? ex = null,
        bool? isTransient = null,
        string? suggestion = null)
        => ToolResult<TValue, ToolError>.Fail(
            new ToolError(message, suggestion),
            ex,
            isTransient);

    /// <summary>
    /// Creates a failure <see cref="ToolResult{TValue, TError}"/> with a custom error shape.
    /// </summary>
    public static ToolResult<TValue, TError> Fail<TValue, TError>(
        TError error,
        Exception? ex = null,
        bool? isTransient = null)
        where TError : class
        => ToolResult<TValue, TError>.Fail(error, ex, isTransient);

    /// <summary>
    /// Creates an <see cref="IToolResult"/> for an unhandled exception caught by middleware.
    /// <see cref="IToolResult.IsTransient"/> is <see langword="null"/> because middleware
    /// cannot determine whether the failure is transient without domain knowledge.
    /// </summary>
    public static IToolResult UnhandledFailure(Exception ex)
        => new UnhandledToolResult(ex);

    private sealed class UnhandledToolResult : IToolResult
    {
        private readonly Exception _ex;

        public UnhandledToolResult(Exception ex) => _ex = ex;

        public bool IsSuccess => false;
        public object? BoxedValue => null;
        public object? BoxedError => new ToolError("An unexpected error occurred. Please try again.");
        public Exception? Exception => _ex;
        public bool? IsTransient => null;
    }
}
