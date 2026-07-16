using System.Text.Json;

namespace LangfuseConformanceApp;

/// <summary>
/// Carries one local or hosted case through the converged Langfuse experiment-runner example.
/// </summary>
internal sealed record LangfuseRunnerCase(
    string Id,
    JsonElement Input,
    JsonElement? ExpectedOutput);
