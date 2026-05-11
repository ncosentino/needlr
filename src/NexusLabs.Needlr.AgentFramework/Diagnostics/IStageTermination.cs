using System.Text.Json.Serialization;

namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Contract for any value that describes why a pipeline stage terminated.
/// Implemented by the framework's closed <see cref="StageTermination"/> hierarchy
/// (12 framework cases including the <see cref="StageTermination.Custom"/> escape
/// hatch) and — optionally — by consumer-defined types when a typed extension
/// case is required.
/// </summary>
/// <remarks>
/// <para>
/// <b>Framework cases — the easy path.</b> Most consumers should use the framework's
/// <see cref="StageTermination"/> hierarchy directly. The 11 typed framework cases
/// cover every loop-natural, loop-bounded, external, and stage-level termination the
/// runner produces; the <see cref="StageTermination.Custom"/> case carries
/// app-specific narrative with a <c>Reason</c> string and an optional structured
/// <c>Properties</c> dictionary. Framework cases are JSON-polymorphism-registered
/// on this interface, so they round-trip through <see cref="System.Text.Json.JsonSerializer"/>
/// out of the box.
/// </para>
/// <para>
/// <b>Custom cases — the typed extension path.</b> Consumers who need a typed
/// extension case (with named-record pattern matching like
/// <c>is MyDomainTermination { FindingCount: var c }</c>) can implement this
/// interface directly:
/// </para>
/// <code>
/// public sealed record MyDomainTermination(int FindingCount) : IStageTermination
/// {
///     public string ToTagValue() =&gt; "MyDomain";
/// }
/// </code>
/// <para>
/// Implementing this interface is a contract: <b>you own the JSON serialization</b>
/// for your derived type. The framework's <see cref="JsonPolymorphicAttribute"/>
/// registry on this interface only knows about the framework cases — your type
/// is not in it. Two practical options:
/// </para>
/// <list type="number">
///   <item>
///     Register your derived type at the JSON layer via a
///     <see cref="System.Text.Json.Serialization.Metadata.JsonTypeInfoResolver"/>
///     modifier so it joins the polymorphism table for that
///     <see cref="System.Text.Json.JsonSerializerOptions"/> instance.
///   </item>
///   <item>
///     Provide your own
///     <see cref="System.Text.Json.Serialization.JsonConverter{T}"/> for
///     <see cref="IStageTermination"/> with full control over the wire format.
///   </item>
/// </list>
/// <para>
/// If you do neither, <c>JsonSerializer.Serialize&lt;IStageTermination&gt;(yourInstance)</c>
/// throws <see cref="System.NotSupportedException"/> — same loud failure the
/// framework cases have today for unregistered types. There is no silent
/// data loss, but every JSON-serialising downstream consumer needs the
/// registration. <b>If you don't want to maintain a JSON registration, use
/// <see cref="StageTermination.Custom"/> instead</b> — it gives you a stable
/// wire format at the cost of dictionary-typed properties.
/// </para>
/// <para>
/// <see cref="StageTermination"/> itself is closed for external derivation at
/// compile time (its constructor is <see langword="internal"/>) — the framework
/// hierarchy is exhaustive by design. Extension happens by implementing this
/// interface directly, not by inheriting from the abstract record.
/// </para>
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$kind")]
[JsonDerivedType(typeof(StageTermination.Completed), nameof(StageTermination.Completed))]
[JsonDerivedType(typeof(StageTermination.NaturalCompletion), nameof(StageTermination.NaturalCompletion))]
[JsonDerivedType(typeof(StageTermination.CompletedEarlyAfterToolCall), nameof(StageTermination.CompletedEarlyAfterToolCall))]
[JsonDerivedType(typeof(StageTermination.MaxIterationsReached), nameof(StageTermination.MaxIterationsReached))]
[JsonDerivedType(typeof(StageTermination.MaxToolCallsReached), nameof(StageTermination.MaxToolCallsReached))]
[JsonDerivedType(typeof(StageTermination.BudgetPressure), nameof(StageTermination.BudgetPressure))]
[JsonDerivedType(typeof(StageTermination.StallDetected), nameof(StageTermination.StallDetected))]
[JsonDerivedType(typeof(StageTermination.Cancelled), nameof(StageTermination.Cancelled))]
[JsonDerivedType(typeof(StageTermination.Failed), nameof(StageTermination.Failed))]
[JsonDerivedType(typeof(StageTermination.Skipped), nameof(StageTermination.Skipped))]
[JsonDerivedType(typeof(StageTermination.Custom), nameof(StageTermination.Custom))]
public interface IStageTermination
{
    /// <summary>
    /// Returns a stable, low-cardinality string suitable for OpenTelemetry tag values
    /// (e.g. the <c>termination_cause</c> tag emitted by
    /// <see cref="IPipelineMetrics"/>). Framework cases return their case name
    /// (e.g. <c>"MaxIterationsReached"</c>); <see cref="StageTermination.Custom"/>
    /// returns its <see cref="StageTermination.Custom.Reason"/> field. Third-party
    /// implementations are responsible for their own cardinality discipline —
    /// the returned string is used directly as a metric tag value.
    /// </summary>
    string ToTagValue();
}
