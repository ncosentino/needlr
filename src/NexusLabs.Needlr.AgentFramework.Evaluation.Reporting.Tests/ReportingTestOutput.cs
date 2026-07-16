using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Reporting.Tests;

internal sealed record ReportingTestOutput(
    IReadOnlyList<ChatMessage> Messages,
    ChatResponse Response);
