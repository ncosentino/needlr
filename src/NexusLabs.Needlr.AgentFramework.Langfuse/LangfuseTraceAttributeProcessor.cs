using System.Diagnostics;
using System.Text.Json;

using OpenTelemetry;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Enriches spans with Langfuse-specific attributes as they flow through the export pipeline.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>
/// Copies <see cref="Activity.Baggage"/> entries (trace-level <c>session.id</c> / <c>user.id</c>
/// set on the scenario root) onto each span's tags so per-observation filtering works.
/// </item>
/// <item>
/// Sets <c>langfuse.observation.type</c> explicitly — <c>generation</c> for chat-completion spans
/// and <c>span</c> for tool-call spans — instead of relying on Langfuse's implicit
/// "has a model attribute ⇒ generation" inference.
/// </item>
/// <item>
/// Projects Needlr's <c>gen_ai.usage.*</c> tags into a <c>langfuse.observation.usage_details</c>
/// JSON attribute so token usage (including <c>cache_read_input_tokens</c> and
/// <c>reasoning_tokens</c>) reliably lands rather than depending on auto-mapping of non-standard
/// keys.
/// </item>
/// </list>
/// <para>
/// This type derives from the OpenTelemetry SDK's <see cref="BaseProcessor{T}"/>, which is the
/// only supported extension point for span processing; subclassing it is a framework requirement,
/// not a design choice. It is registered before the OTLP exporter so its <see cref="OnEnd"/>
/// mutations are visible to the exporter.
/// </para>
/// </remarks>
internal sealed class LangfuseTraceAttributeProcessor : BaseProcessor<Activity>
{
    private const string ChatActivityName = "agent.chat";
    private const string ChatStreamActivityName = "agent.chat.stream";
    private const string ToolActivityPrefix = "agent.tool";

    private readonly string? _environment;
    private readonly string? _release;

    /// <summary>
    /// Initializes the processor with optional trace-level context propagated to every span.
    /// </summary>
    /// <param name="environment">
    /// The Langfuse environment emitted as <c>langfuse.environment</c>, or <see langword="null"/>
    /// to leave it unset.
    /// </param>
    /// <param name="release">
    /// The release identifier emitted as <c>langfuse.release</c>, or <see langword="null"/> to
    /// leave it unset.
    /// </param>
    public LangfuseTraceAttributeProcessor(string? environment = null, string? release = null)
    {
        _environment = string.IsNullOrWhiteSpace(environment) ? null : environment;
        _release = string.IsNullOrWhiteSpace(release) ? null : release;
    }

    /// <inheritdoc />
    public override void OnStart(Activity data)
    {
        ArgumentNullException.ThrowIfNull(data);

        CopyBaggageToTags(data);
        SetObservationType(data);
        SetContextAttributes(data);
    }

    /// <inheritdoc />
    public override void OnEnd(Activity data)
    {
        ArgumentNullException.ThrowIfNull(data);

        SetUsageDetails(data);
    }

    private void SetContextAttributes(Activity data)
    {
        if (_environment is not null && data.GetTagItem("langfuse.environment") is null)
        {
            data.SetTag("langfuse.environment", _environment);
        }

        if (_release is not null && data.GetTagItem("langfuse.release") is null)
        {
            data.SetTag("langfuse.release", _release);
        }
    }

    private static void CopyBaggageToTags(Activity data)
    {
        foreach (var entry in data.Baggage)
        {
            if (entry.Value is not null && data.GetTagItem(entry.Key) is null)
            {
                data.SetTag(entry.Key, entry.Value);
            }
        }
    }

    private static void SetObservationType(Activity data)
    {
        if (data.GetTagItem("langfuse.observation.type") is not null)
        {
            return;
        }

        var type = data.OperationName switch
        {
            ChatActivityName or ChatStreamActivityName => "generation",
            _ when data.OperationName.StartsWith(ToolActivityPrefix, StringComparison.Ordinal) => "span",
            _ => null,
        };

        if (type is not null)
        {
            data.SetTag("langfuse.observation.type", type);
        }
    }

    private static void SetUsageDetails(Activity data)
    {
        if (data.GetTagItem("langfuse.observation.usage_details") is not null)
        {
            return;
        }

        var input = ReadTokenTag(data, "gen_ai.usage.input_tokens");
        var output = ReadTokenTag(data, "gen_ai.usage.output_tokens");
        var cacheRead = ReadTokenTag(data, "gen_ai.usage.cached_input_tokens");
        var reasoning = ReadTokenTag(data, "gen_ai.usage.reasoning_tokens");

        if (input is null && output is null && cacheRead is null && reasoning is null)
        {
            return;
        }

        var usage = new Dictionary<string, long>(StringComparer.Ordinal);
        if (input is { } i) usage["input"] = i;
        if (output is { } o) usage["output"] = o;
        if (cacheRead is { } c) usage["cache_read_input_tokens"] = c;
        if (reasoning is { } r) usage["reasoning_tokens"] = r;
        if (input is { } ti && output is { } to) usage["total"] = ti + to;

        data.SetTag("langfuse.observation.usage_details", JsonSerializer.Serialize(usage));
    }

    private static long? ReadTokenTag(Activity data, string key) => data.GetTagItem(key) switch
    {
        null => null,
        long l => l,
        int n => n,
        short s => s,
        byte b => b,
        string str when long.TryParse(str, out var parsed) => parsed,
        _ => null,
    };
}
