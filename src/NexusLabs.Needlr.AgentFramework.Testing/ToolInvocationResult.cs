using NexusLabs.Needlr.AgentFramework.Workspace;

namespace NexusLabs.Needlr.AgentFramework.Testing;

/// <summary>
/// Result of a single <see cref="ToolInvocationRunner.InvokeAsync{TTool}(string, Action{Microsoft.Extensions.AI.AIFunctionArguments}?, System.Threading.CancellationToken)"/>
/// call.
/// </summary>
/// <param name="ReturnValue">
/// The raw return value from the underlying <see cref="Microsoft.Extensions.AI.AIFunction.InvokeAsync"/>
/// call, or <see langword="null"/> if the invocation threw.
/// </param>
/// <param name="Exception">
/// The exception thrown during invocation, or <see langword="null"/> if the invocation succeeded.
/// Exceptions from argument extraction (the source-generated wrapper) and from the user method
/// itself are not currently distinguished; both surface here.
/// </param>
/// <param name="FunctionSource">
/// Which discovery path produced the <see cref="Microsoft.Extensions.AI.AIFunction"/> that was
/// invoked. Use this in assertions to confirm the test exercised the source-generated wrapper
/// rather than the reflection fallback.
/// </param>
/// <param name="Workspace">
/// The <see cref="IWorkspace"/> attached to the execution context for the invocation, or
/// <see langword="null"/> if no workspace was configured. Useful for post-invocation
/// assertions on files the tool wrote.
/// </param>
/// <param name="Duration">Wall-clock duration of the invocation.</param>
public sealed record ToolInvocationResult(
    object? ReturnValue,
    Exception? Exception,
    ToolFunctionSource FunctionSource,
    IWorkspace? Workspace,
    TimeSpan Duration)
{
    /// <summary>
    /// Whether the invocation completed without throwing.
    /// </summary>
    public bool Succeeded => Exception is null;

    /// <summary>
    /// Returns <see cref="ReturnValue"/> cast to <typeparamref name="T"/>, or
    /// <see langword="default"/> if the value is <see langword="null"/> or not assignable to
    /// <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">Expected return type.</typeparam>
    public T? GetValue<T>() => ReturnValue is T typed ? typed : default;

    /// <summary>
    /// Throws if the invocation failed, surfacing the original exception with context.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <see cref="Exception"/> is not <see langword="null"/>, with the original
    /// exception attached as the inner exception.
    /// </exception>
    public void AssertSuccess()
    {
        if (Exception is not null)
        {
            throw new InvalidOperationException(
                $"Tool invocation failed via {FunctionSource} path: {Exception.Message}",
                Exception);
        }
    }

    /// <summary>
    /// Throws if the string form of <see cref="ReturnValue"/> does not contain
    /// <paramref name="substring"/>.
    /// </summary>
    /// <param name="substring">The substring expected to appear in the return value.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the return value is <see langword="null"/> or does not contain the substring.
    /// </exception>
    public void AssertResultContains(string substring)
    {
        ArgumentNullException.ThrowIfNull(substring);

        var text = ReturnValue?.ToString();
        if (text is null || !text.Contains(substring, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Expected tool return value to contain '{substring}'. Actual: '{text ?? "<null>"}'.");
        }
    }
}
