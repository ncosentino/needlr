namespace NexusLabs.Needlr.AgentFramework;

/// <summary>
/// Marks an agent class as a member of a named sequential pipeline, specifying its position.
/// The source generator reads these declarations to emit a strongly-typed
/// <c>Create{PipelineName}SequentialWorkflow()</c> extension method on <see cref="IWorkflowFactory"/>.
/// </summary>
/// <remarks>
/// Apply multiple instances of this attribute on different agent classes with the same
/// <paramref name="pipelineName"/> to declare the full sequence. Agents are executed in ascending
/// <paramref name="order"/> — output from each agent is passed as input to the next.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class AgentSequenceMemberAttribute(string pipelineName, int order) : Attribute
{
    /// <summary>The name of the sequential pipeline this agent belongs to.</summary>
    public string PipelineName { get; } = pipelineName;

    /// <summary>The zero-based position of this agent within the pipeline sequence.</summary>
    public int Order { get; } = order;
}
