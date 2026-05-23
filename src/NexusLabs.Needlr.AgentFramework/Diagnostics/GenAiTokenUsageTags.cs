namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Strongly-typed bag of optional tags attached to a <see cref="IGenAiTokenMetrics.RecordTokenUsage"/>
/// measurement. Mirrors the tag schema written by MEAI's
/// <see cref="Microsoft.Extensions.AI.OpenTelemetryChatClient"/> on the same
/// <c>gen_ai.client.token.usage</c> histogram so the resulting series share a label set
/// and aggregate cleanly under a single OpenTelemetry <c>MetricStreamIdentity</c>.
/// </summary>
/// <remarks>
/// <para>
/// Any property whose value is <see langword="null"/> is omitted from the recorded measurement.
/// This matches MEAI's behaviour, where MEAI also omits tags whose source data is unavailable.
/// </para>
/// <para>
/// <see cref="ServerPort"/> SHOULD be supplied whenever <see cref="ServerAddress"/> is
/// supplied, matching MEAI's behaviour of always emitting <c>server.port</c> alongside
/// <c>server.address</c> with no scheme-default special-casing.
/// </para>
/// </remarks>
/// <param name="OperationName">
/// Value for the <c>gen_ai.operation.name</c> tag, or <see langword="null"/> to use the
/// default <c>"chat"</c>. Note: <c>new GenAiTokenUsageTags()</c> (parameterless
/// invocation) yields <see langword="null"/> here even though the primary constructor
/// declares a default — that is the standard C# record-struct behaviour for the
/// implicit parameterless constructor. The implementation substitutes <c>"chat"</c> in
/// either case so callers can use <c>default</c>/<c>new GenAiTokenUsageTags()</c>
/// without surprise.
/// </param>
/// <param name="RequestModel">Value for the <c>gen_ai.request.model</c> tag, or <see langword="null"/> to omit.</param>
/// <param name="ResponseModel">Value for the <c>gen_ai.response.model</c> tag, or <see langword="null"/> to omit.</param>
/// <param name="ProviderName">Value for the <c>gen_ai.provider.name</c> tag, or <see langword="null"/> to omit.</param>
/// <param name="ServerAddress">Value for the <c>server.address</c> tag, or <see langword="null"/> to omit.</param>
/// <param name="ServerPort">Value for the <c>server.port</c> tag, or <see langword="null"/> to omit.</param>
public readonly record struct GenAiTokenUsageTags(
    string? OperationName = null,
    string? RequestModel = null,
    string? ResponseModel = null,
    string? ProviderName = null,
    string? ServerAddress = null,
    int? ServerPort = null);
