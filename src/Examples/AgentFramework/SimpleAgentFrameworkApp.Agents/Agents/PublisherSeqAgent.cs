using NexusLabs.Needlr.AgentFramework;

namespace SimpleAgentFrameworkApp.Agents;

/// <summary>
/// Third stage in the "content-pipeline" sequential workflow.
/// Formats the edited content for publication.
/// </summary>
[NeedlrAiAgent(
    Description = "Formats content for publication, adding structure and polish.",
    Instructions = """
        You are a content publisher. Take the edited text you receive and format it for
        publication: add an engaging title, break into short paragraphs if needed, and
        end with a one-sentence call-to-action. Return only the formatted output.
        """,
    FunctionTypes = new Type[0])]
[AgentSequenceMember("content-pipeline", 3)]
public partial class PublisherSeqAgent { }
