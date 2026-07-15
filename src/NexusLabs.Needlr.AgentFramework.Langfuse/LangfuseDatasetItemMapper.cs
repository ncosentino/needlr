using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Maps one hosted Langfuse dataset item to a provider-neutral experiment case.
/// </summary>
/// <typeparam name="TCase">The caller-owned case value type.</typeparam>
/// <param name="item">The hosted dataset item and provider references.</param>
/// <returns>The experiment case. Its id must equal <see cref="LangfuseDatasetItemSnapshot.Id"/>.</returns>
public delegate ExperimentCase<TCase> LangfuseDatasetItemMapper<TCase>(
    LangfuseDatasetItemSnapshot item);
