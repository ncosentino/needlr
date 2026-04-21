namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Discriminates between agent nodes (backed by an LLM) and reducer nodes
/// (pure deterministic functions) in a DAG workflow result.
/// </summary>
public enum NodeKind
{
    /// <summary>The node is an LLM-backed agent.</summary>
    Agent,

    /// <summary>The node is a deterministic reducer function (no LLM calls).</summary>
    Reducer,
}
