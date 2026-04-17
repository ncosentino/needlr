namespace NexusLabs.Needlr.AgentFramework.Workspace;

/// <summary>
/// Result of a workspace operation — either a success value of type
/// <typeparamref name="T"/> or a failure carrying an <see cref="System.Exception"/>.
/// </summary>
/// <typeparam name="T">The operation-specific success data type.</typeparam>
public sealed class WorkspaceResult<T>
{
    /// <summary>Whether the operation succeeded.</summary>
    public bool Success { get; }

    /// <summary>
    /// The success value. Only meaningful when <see cref="Success"/> is
    /// <see langword="true"/>.
    /// </summary>
    public T Value { get; }

    /// <summary>
    /// The exception that caused the failure. Only meaningful when
    /// <see cref="Success"/> is <see langword="false"/>.
    /// </summary>
    public Exception? Exception { get; }

    private WorkspaceResult(T value)
    {
        Success = true;
        Value = value;
    }

    private WorkspaceResult(Exception exception)
    {
        Success = false;
        Exception = exception;
        Value = default!;
    }

    /// <summary>Creates a success result carrying <paramref name="value"/>.</summary>
    public static WorkspaceResult<T> Ok(T value) => new(value);

    /// <summary>Creates a failure result carrying <paramref name="exception"/>.</summary>
    public static WorkspaceResult<T> Fail(Exception exception) => new(exception);
}
