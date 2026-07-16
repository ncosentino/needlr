using Microsoft.Extensions.AI;

namespace MeaiReportingExperimentApp;

/// <summary>
/// Carries the model request and response into the Reporting item evaluator.
/// </summary>
internal sealed record ReportingExampleOutput(
    IReadOnlyList<ChatMessage> Messages,
    ChatResponse Response);
