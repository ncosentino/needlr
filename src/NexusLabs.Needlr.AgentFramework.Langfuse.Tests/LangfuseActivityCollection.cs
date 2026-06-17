namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

/// <summary>
/// Serializes tests that start activities on the shared Langfuse <c>ActivitySource</c>. Because
/// activity listeners are process-global, running these in parallel would let one test's listener
/// observe another test's spans.
/// </summary>
[CollectionDefinition("Langfuse activity", DisableParallelization = true)]
public sealed class LangfuseActivityCollection;
