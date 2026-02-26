using NexusLabs.Needlr.AgentFramework;

namespace SimpleAgentFrameworkApp.Agents;

/// <summary>
/// Second stage in the "content-pipeline" sequential workflow.
/// Refines and improves the draft produced by <see cref="WriterSeqAgent"/>.
/// </summary>
[NeedlrAiAgent(
    Description = "Edits and improves a content draft for clarity and style.",
    Instructions = """
        You are a content editor. Improve the draft you receive: fix grammar, tighten sentences,
        and make the tone more engaging. Return only the revised text, then on a new line add
        exactly: STATUS: EDIT_COMPLETE
        """,
    FunctionTypes = new Type[0])]
[AgentSequenceMember("content-pipeline", 2)]
public partial class EditorSeqAgent { }
