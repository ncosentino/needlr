namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Provides stable machine-actionable failure data without retaining a raw exception or stack.
/// </summary>
public sealed record ExperimentFailure
{
    /// <summary>
    /// Initializes a validated structured failure.
    /// </summary>
    /// <param name="code">The stable failure code.</param>
    /// <param name="stage">The stage that produced the failure.</param>
    /// <param name="exceptionType">The originating exception type name.</param>
    /// <param name="message">The exception message. An empty message is permitted.</param>
    /// <param name="isRetryable">
    /// A value indicating whether the active retry policy selected another attempt after this
    /// failure.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="code"/> or <paramref name="stage"/> is not a defined enumeration value.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="exceptionType"/> is <see langword="null"/>, empty, or white space.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="message"/> is <see langword="null"/>.
    /// </exception>
    public ExperimentFailure(
        ExperimentFailureCode code,
        ExperimentFailureStage stage,
        string exceptionType,
        string message,
        bool isRetryable)
    {
        if (!Enum.IsDefined(code))
        {
            throw new ArgumentOutOfRangeException(
                nameof(code),
                code,
                "The experiment failure code is not defined.");
        }

        if (!Enum.IsDefined(stage))
        {
            throw new ArgumentOutOfRangeException(
                nameof(stage),
                stage,
                "The experiment failure stage is not defined.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(exceptionType);
        ArgumentNullException.ThrowIfNull(message);
        Code = code;
        Stage = stage;
        ExceptionType = exceptionType;
        Message = message;
        IsRetryable = isRetryable;
    }

    /// <summary>Gets the stable failure code.</summary>
    public ExperimentFailureCode Code { get; }

    /// <summary>Gets the stage that produced the failure.</summary>
    public ExperimentFailureStage Stage { get; }

    /// <summary>Gets the originating exception type name.</summary>
    public string ExceptionType { get; }

    /// <summary>Gets the exception message.</summary>
    public string Message { get; }

    /// <summary>
    /// Gets a value indicating whether the active retry policy selected another attempt after this
    /// failure.
    /// </summary>
    public bool IsRetryable { get; }
}
