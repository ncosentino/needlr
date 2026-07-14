namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Provides stable machine-actionable failure data without retaining a raw exception or stack.
/// </summary>
public sealed class ExperimentFailure
{
    /// <summary>Gets the stable failure code.</summary>
    public required ExperimentFailureCode Code { get; init; }

    /// <summary>Gets the stage that produced the failure.</summary>
    public required ExperimentFailureStage Stage { get; init; }

    /// <summary>Gets the originating exception type name.</summary>
    public required string ExceptionType { get; init; }

    /// <summary>Gets the exception message.</summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets a value indicating whether the active retry policy selected this failure. Phase 1
    /// always reports <see langword="false"/>.
    /// </summary>
    public bool IsRetryable { get; init; }
}
