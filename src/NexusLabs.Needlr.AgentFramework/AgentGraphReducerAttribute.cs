namespace NexusLabs.Needlr.AgentFramework;

/// <summary>
/// Declares a deterministic reducer node for fan-in convergence in a named
/// graph workflow. The reducer aggregates branch outputs without incurring
/// LLM cost — it is a pure function, not an agent.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="ReducerMethod"/> must be a <c>public static</c> method on the
/// decorated class that accepts <see cref="System.Collections.Generic.IReadOnlyList{T}"/>
/// of <see cref="string"/> and returns <see cref="string"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [AgentGraphReducer("ResearchPipeline", ReducerMethod = nameof(MergeResults))]
/// public static class ResearchReducer
/// {
///     public static string MergeResults(IReadOnlyList&lt;string&gt; branchOutputs)
///         =&gt; string.Join("\n---\n", branchOutputs);
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class AgentGraphReducerAttribute : Attribute
{
    /// <summary>
    /// Initializes a new <see cref="AgentGraphReducerAttribute"/>.
    /// </summary>
    /// <param name="graphName">The name of the graph this reducer belongs to.</param>
    public AgentGraphReducerAttribute(string graphName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(graphName);
        GraphName = graphName;
    }

    /// <summary>Gets the graph name this reducer belongs to.</summary>
    public string GraphName { get; }

    /// <summary>
    /// Gets or sets the name of the static method on the decorated class that
    /// performs the reduction. The method must accept
    /// <see cref="System.Collections.Generic.IReadOnlyList{T}"/> of
    /// <see cref="string"/> and return <see cref="string"/>.
    /// </summary>
    /// <remarks>
    /// If not specified, defaults to <c>"Reduce"</c> by convention. The source
    /// generator will look for a <c>public static string Reduce(IReadOnlyList&lt;string&gt;)</c>
    /// method on the decorated class.
    /// </remarks>
    public string? ReducerMethod { get; set; }
}
