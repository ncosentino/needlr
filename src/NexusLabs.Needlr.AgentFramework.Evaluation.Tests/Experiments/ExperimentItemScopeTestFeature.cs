namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests.Experiments;

/// <summary>
/// Carries a stable feature value and activation-local state for item-scope tests.
/// </summary>
internal sealed class ExperimentItemScopeTestFeature(string value)
{
    public string Value { get; } = value;

    public AsyncLocal<string?> AmbientValue { get; } = new();
}
