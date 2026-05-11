using System.Text.Json.Serialization;

namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Describes why a pipeline stage terminated. Framework cases carry structured
/// metadata (e.g. <see cref="MaxIterationsReached"/> knows the limit and the iterations
/// used); application narrative goes through <see cref="Custom"/>.
/// </summary>
/// <remarks>
/// <para>
/// Use pattern matching to inspect the termination at a stage:
/// </para>
/// <code>
/// if (stage.Termination is StageTermination.MaxToolCallsReached { Limit: var limit })
/// {
///     _logger.LogWarning("Stage exceeded {Limit} tool calls", limit);
/// }
/// </code>
/// <para>
/// For OpenTelemetry / Prometheus tag values, use <see cref="ToTagValue"/> — it
/// returns a stable, low-cardinality string suitable for use as a metric dimension.
/// Framework cases return their case name (e.g. <c>"MaxIterationsReached"</c>);
/// <see cref="Custom"/> returns its <see cref="Custom.Reason"/> field directly. If
/// the consumer's <see cref="Custom"/> reasons are high-cardinality, the consumer is
/// responsible for bucketing before recording the tag.
/// </para>
/// <para>
/// JSON serialization is supported via <see cref="JsonPolymorphicAttribute"/> with
/// the <c>$kind</c> discriminator. Each derived type is registered with its case name
/// (e.g. <c>"MaxIterationsReached"</c>) as the discriminator value. Once shipped,
/// the JSON wire format is part of the API surface — renaming or removing a case
/// breaks the wire format.
/// </para>
/// <para>
/// Adding a new framework case in a future release is a soft break for consumers
/// using exhaustive switch <em>expressions</em> — they will need to add a default
/// arm. Switch statements with no return value continue to work without modification.
/// </para>
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$kind")]
[JsonDerivedType(typeof(Completed), nameof(Completed))]
[JsonDerivedType(typeof(NaturalCompletion), nameof(NaturalCompletion))]
[JsonDerivedType(typeof(CompletedEarlyAfterToolCall), nameof(CompletedEarlyAfterToolCall))]
[JsonDerivedType(typeof(MaxIterationsReached), nameof(MaxIterationsReached))]
[JsonDerivedType(typeof(MaxToolCallsReached), nameof(MaxToolCallsReached))]
[JsonDerivedType(typeof(BudgetPressure), nameof(BudgetPressure))]
[JsonDerivedType(typeof(StallDetected), nameof(StallDetected))]
[JsonDerivedType(typeof(Cancelled), nameof(Cancelled))]
[JsonDerivedType(typeof(Failed), nameof(Failed))]
[JsonDerivedType(typeof(Skipped), nameof(Skipped))]
[JsonDerivedType(typeof(Custom), nameof(Custom))]
public abstract record StageTermination : IStageTermination
{
    /// <summary>
    /// Internal constructor closes the hierarchy for external derivation. Consumers
    /// who need a typed extension case implement <see cref="IStageTermination"/>
    /// directly rather than inheriting from this abstract record (see the interface's
    /// XML doc for the contract). The framework's nested case records still chain
    /// through this constructor because they live in the same assembly.
    /// </summary>
    internal StageTermination() { }

    /// <summary>
    /// The stage's <see cref="Iterative.IterativeLoopOptions.IsComplete"/> predicate
    /// returned <see langword="true"/> after an iteration — the stage achieved its goal.
    /// </summary>
    public sealed record Completed : StageTermination;

    /// <summary>
    /// The model produced a text response without requesting tool calls, signalling
    /// natural completion of the task.
    /// </summary>
    public sealed record NaturalCompletion : StageTermination;

    /// <summary>
    /// The <see cref="Iterative.IterativeLoopOptions.IsComplete"/> predicate returned
    /// <see langword="true"/> after a tool call within an iteration. This is a success
    /// termination — the stage achieved its goal and the loop exited early to avoid a
    /// wasted chat completion call.
    /// </summary>
    public sealed record CompletedEarlyAfterToolCall : StageTermination;

    /// <summary>
    /// The loop exhausted its configured iteration limit without the
    /// <see cref="Iterative.IterativeLoopOptions.IsComplete"/> predicate returning
    /// <see langword="true"/>.
    /// </summary>
    /// <param name="Limit">The configured iteration limit.</param>
    /// <param name="IterationsUsed">How many iterations actually ran.</param>
    public sealed record MaxIterationsReached(int Limit, int IterationsUsed) : StageTermination;

    /// <summary>
    /// The cumulative tool-call count across all iterations exceeded the configured
    /// <see cref="Iterative.IterativeLoopOptions.MaxTotalToolCalls"/> limit.
    /// </summary>
    /// <param name="Limit">The configured cumulative tool-call limit.</param>
    /// <param name="ToolCallsUsed">How many tool calls actually ran across all iterations.</param>
    public sealed record MaxToolCallsReached(int Limit, int ToolCallsUsed) : StageTermination;

    /// <summary>
    /// The token budget tracker reported usage above the configured budget pressure
    /// threshold, and the loop ran one final finalization iteration before terminating.
    /// </summary>
    /// <param name="Threshold">
    /// The configured budget pressure threshold (0.0–1.0), or <see langword="null"/>
    /// if no threshold was configured but pressure was reported through other means.
    /// </param>
    public sealed record BudgetPressure(double? Threshold) : StageTermination;

    /// <summary>
    /// The loop detected that consecutive iterations produced nearly identical token
    /// usage, indicating the LLM is repeating the same work without making progress.
    /// </summary>
    /// <param name="ConsecutiveThreshold">
    /// The number of consecutive stalled iterations the loop required before terminating,
    /// or <see langword="null"/> if the configuration was not snapshot at termination time.
    /// </param>
    public sealed record StallDetected(int? ConsecutiveThreshold) : StageTermination;

    /// <summary>
    /// The loop was cancelled via <see cref="System.Threading.CancellationToken"/>.
    /// </summary>
    public sealed record Cancelled : StageTermination;

    /// <summary>
    /// The stage threw an exception, OR the iterative loop reported an unrecoverable
    /// error (in which case the loop's error message is wrapped in an
    /// <see cref="System.InvalidOperationException"/> here).
    /// </summary>
    /// <param name="Exception">The exception that caused the failure.</param>
    public sealed record Failed(Exception Exception) : StageTermination;

    /// <summary>
    /// The stage was skipped via a <c>ShouldSkip</c> predicate on
    /// <c>StageExecutionPolicy</c>. <see cref="Reason"/> is optional because the
    /// current predicate signature does not supply one.
    /// </summary>
    /// <param name="Reason">
    /// Optional free-form description of why the stage was skipped. <see langword="null"/>
    /// when the runner has no detail to supply.
    /// </param>
    public sealed record Skipped(string? Reason = null) : StageTermination;

    /// <summary>
    /// Application-specific termination cause not covered by framework cases. Use this
    /// from an <c>onLoopCompleted</c> callback when a stage has app-specific semantics
    /// (e.g. <c>"Reconciled — 7 outstanding issues"</c>).
    /// </summary>
    /// <param name="Reason">
    /// Short, dashboard-friendly description. Used as the OpenTelemetry tag value via
    /// <see cref="ToTagValue"/>, so consumers should keep the cardinality bounded
    /// (or normalise / bucket before constructing the case).
    /// </param>
    /// <param name="Properties">
    /// Optional structured metadata for richer post-mortem queries. Values typed as
    /// <see cref="object"/> for flexibility — note that JSON deserialization yields
    /// <see cref="System.Text.Json.JsonElement"/> values, not the original concrete types.
    /// </param>
    public sealed record Custom(string Reason, IReadOnlyDictionary<string, object?>? Properties = null) : StageTermination
    {
        /// <inheritdoc/>
        public override string ToTagValue() => Reason;
    }

    /// <summary>
    /// Returns a stable, low-cardinality string suitable for OpenTelemetry tag values.
    /// Default implementation returns the case name (e.g. <c>"MaxIterationsReached"</c>).
    /// <see cref="Custom"/> overrides this to return its <see cref="Custom.Reason"/> field.
    /// </summary>
    public virtual string ToTagValue() => GetType().Name;
}
