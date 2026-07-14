using System.Text.Json.Serialization;

namespace ExperimentRunnerApp;

/// <summary>
/// Provides Native AOT-safe serialization metadata for caller-owned experiment payloads.
/// </summary>
[JsonSerializable(typeof(ExperimentCaseDefinition))]
[JsonSerializable(typeof(ExperimentOutput))]
internal sealed partial class ExperimentJsonContext : JsonSerializerContext;
