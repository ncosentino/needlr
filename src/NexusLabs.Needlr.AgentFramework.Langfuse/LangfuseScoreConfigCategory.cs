namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// One allowed category for a <see cref="LangfuseScoreDataType.Categorical"/> score config.
/// </summary>
/// <param name="Label">The human-readable label shown in Langfuse.</param>
/// <param name="Value">The numeric value the label maps to.</param>
public sealed record LangfuseScoreConfigCategory(string Label, double Value);
