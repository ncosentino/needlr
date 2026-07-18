using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

/// <summary>
/// Supplies one dynamically produced artifact to conformance evaluators.
/// </summary>
[DoNotAutoRegister]
internal sealed class ConformanceEvaluationContext(string artifact)
    : EvaluationContext("artifact", [])
{
    public string Artifact { get; } = artifact;
}
