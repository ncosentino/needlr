using NexusLabs.Needlr.AgentFramework.Progress;

namespace NexusLabs.Needlr.AgentFramework;

/// <summary>
/// Declares which <see cref="IProgressSink"/> types should receive progress events when
/// this agent runs. The source generator discovers this attribute and emits a companion
/// method that returns the sink types for use by orchestrators when creating reporters.
/// </summary>
/// <remarks>
/// <para>
/// Apply alongside <see cref="NeedlrAiAgentAttribute"/> on agent classes:
/// </para>
/// <code>
/// [NeedlrAiAgent(Instructions = "...")]
/// [ProgressSinks(typeof(CostTrackingSink), typeof(AuditSink))]
/// public partial class WriterAgent { }
/// </code>
/// <para>
/// The generator emits a <c>GetWriterAgentProgressSinkTypes()</c> extension method
/// on <c>IAgentFactory</c> that returns the declared types. Orchestrators use this
/// to create reporters with the correct sinks for each agent.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ProgressSinksAttribute : Attribute
{
    /// <summary>Gets the sink types declared for this agent.</summary>
    public Type[] SinkTypes { get; }

    /// <param name="sinkTypes">
    /// The <see cref="IProgressSink"/> types to use for this agent's progress reporting.
    /// </param>
    public ProgressSinksAttribute(params Type[] sinkTypes)
    {
        ArgumentNullException.ThrowIfNull(sinkTypes);
        SinkTypes = sinkTypes;
    }
}
