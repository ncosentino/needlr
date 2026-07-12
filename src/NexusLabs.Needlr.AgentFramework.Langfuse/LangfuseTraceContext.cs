using System.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Carries the explicit Langfuse trace context associated with one scenario to its in-process
/// child activities without placing application data in cross-process OpenTelemetry baggage.
/// </summary>
internal sealed record LangfuseTraceContext
{
    internal const string ActivityPropertyName =
        "NexusLabs.Needlr.AgentFramework.Langfuse.TraceContext";

    public required string Name { get; init; }

    public string? SessionId { get; init; }

    public string? UserId { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public string? Version { get; init; }

    public string? PromptName { get; init; }

    public int? PromptVersion { get; init; }

    public static LangfuseTraceContext? FindNearest(Activity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);

        for (var current = activity; current is not null; current = current.Parent)
        {
            if (current.GetCustomProperty(ActivityPropertyName) is LangfuseTraceContext context)
            {
                return context;
            }
        }

        return null;
    }

    public static void Attach(Activity? activity, LangfuseTraceContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        activity?.SetCustomProperty(ActivityPropertyName, context);
    }
}
