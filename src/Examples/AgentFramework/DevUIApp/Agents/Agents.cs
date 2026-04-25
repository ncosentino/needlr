using NexusLabs.Needlr.AgentFramework;

namespace DevUIApp;

/// <summary>
/// A data lookup assistant that answers questions using the data tools.
/// </summary>
[NeedlrAiAgent(
    Description = "Answers factual questions using the data tools.",
    Instructions = "You are a helpful data assistant. Use the available tools to look up information.",
    FunctionGroups = new[] { "data-tools" })]
public partial class DataAssistant { }

/// <summary>
/// A summarization agent that condenses text.
/// </summary>
[NeedlrAiAgent(
    Description = "Summarizes long text into concise bullet points.",
    Instructions = "You are a summarization expert. Take input text and produce concise bullet points.")]
public partial class SummaryAgent { }
