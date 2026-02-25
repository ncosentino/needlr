using NexusLabs.Needlr.AgentFramework;

namespace SimpleAgentFrameworkApp.Agents;

/// <summary>
/// First stage in the "content-pipeline" sequential workflow.
/// Drafts raw content based on the input prompt.
/// Declared with <see cref="AgentSequenceMemberAttribute"/> so the source generator emits
/// a strongly-typed <c>CreateContentPipelineSequentialWorkflow()</c> extension on
/// <see cref="IWorkflowFactory"/>.
/// </summary>
[NeedlrAiAgent(
    Description = "Drafts raw content for a given topic.",
    Instructions = """
        You are a content writer. Given a topic, produce a short draft (2-3 sentences).
        Be clear and direct. Do not add meta-commentary.
        """,
    FunctionTypes = new Type[0])]
[AgentSequenceMember("content-pipeline", 1)]
public partial class WriterSeqAgent { }
