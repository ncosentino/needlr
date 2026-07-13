using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Configuration for exporting Needlr agent telemetry and evaluation scores to Langfuse.
/// </summary>
/// <remarks>
/// <para>
/// The common path is <see cref="FromEnvironment"/>, which reads the standard Langfuse
/// environment variables. When the public/secret keys are absent the integration disables
/// itself and behaves as a no-op, so credential-less CI runs never fail.
/// </para>
/// <para>
/// Needlr emits OpenTelemetry traces and metrics under well-known source and meter names.
/// The defaults here match <see cref="AgentFrameworkMetricsOptions"/>. If a consumer has
/// customised those names via <c>ConfigureMetrics(...)</c>, set
/// <see cref="AgentActivitySourceName"/>, <see cref="AgentMeterName"/>, and
/// <see cref="GenAiMeterName"/> to the same values, or add them through
/// <see cref="AdditionalActivitySources"/> / <see cref="AdditionalMeters"/>.
/// </para>
/// </remarks>
public sealed class LangfuseOptions
{
    private static readonly AgentFrameworkMetricsOptions MetricsDefaults = new();

    /// <summary>
    /// Environment variable read for the Langfuse public key by <see cref="FromEnvironment"/>.
    /// </summary>
    public const string PublicKeyEnvironmentVariable = "LANGFUSE_PUBLIC_KEY";

    /// <summary>
    /// Environment variable read for the Langfuse secret key by <see cref="FromEnvironment"/>.
    /// </summary>
    public const string SecretKeyEnvironmentVariable = "LANGFUSE_SECRET_KEY";

    /// <summary>
    /// Environment variable read for the Langfuse host (base URL) by <see cref="FromEnvironment"/>.
    /// </summary>
    public const string HostEnvironmentVariable = "LANGFUSE_HOST";

    /// <summary>
    /// Gets or sets the Langfuse public key (<c>pk-lf-...</c>). Required for export.
    /// </summary>
    public string? PublicKey { get; set; }

    /// <summary>
    /// Gets or sets the Langfuse secret key (<c>sk-lf-...</c>). Required for export.
    /// </summary>
    public string? SecretKey { get; set; }

    /// <summary>
    /// Gets or sets the Langfuse base URL (for example <c>https://cloud.langfuse.com</c> or a
    /// self-hosted <c>http://localhost:3000</c>). When set, this takes precedence over
    /// <see cref="Region"/>. When <see langword="null"/>, the URL is derived from
    /// <see cref="Region"/>.
    /// </summary>
    public string? Host { get; set; }

    /// <summary>
    /// Gets or sets the Langfuse Cloud data region. <see langword="null"/> by default — exporting
    /// to a cloud region is therefore an explicit, opt-in choice. Ignored when <see cref="Host"/>
    /// is set.
    /// </summary>
    /// <remarks>
    /// To avoid silently sending traces (which may include prompts, agent outputs, and customer
    /// data) to Langfuse Cloud, this integration requires an <strong>explicit</strong> target: set
    /// <see cref="Host"/> for a self-hosted deployment, or set <see cref="Region"/> to deliberately
    /// opt in to a Langfuse Cloud region. When neither is set, export is disabled even if
    /// credentials are present (see <see cref="IsConfigured"/>) and a message is sent to
    /// <see cref="DiagnosticsCallback"/>.
    /// </remarks>
    public LangfuseRegion? Region { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether export is enabled. When <see langword="true"/>
    /// (the default) export still only occurs if <see cref="PublicKey"/> and
    /// <see cref="SecretKey"/> are both present — see <see cref="IsConfigured"/>. Set to
    /// <see langword="false"/> to force a no-op regardless of credentials.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the OpenTelemetry <c>service.name</c> resource attribute applied to exported
    /// telemetry. Surfaces in Langfuse as the originating service. Defaults to
    /// <c>"needlr-agent"</c>.
    /// </summary>
    public string ServiceName { get; set; } = "needlr-agent";

    /// <summary>
    /// Gets or sets the optional OpenTelemetry <c>service.version</c> resource attribute.
    /// </summary>
    public string? ServiceVersion { get; set; }

    /// <summary>
    /// Gets or sets the Langfuse deployment environment (for example <c>ci</c>, <c>local</c>,
    /// <c>staging</c>, or <c>production</c>). When set, it is emitted as <c>langfuse.environment</c>
    /// on every exported span so Langfuse partitions this run's data — keeping CI eval noise out of
    /// production dashboards. <see langword="null"/> by default (Langfuse uses its <c>default</c>
    /// environment).
    /// </summary>
    public string? Environment { get; set; }

    /// <summary>
    /// Gets or sets the application release identifier (for example a git SHA or semantic version).
    /// When set, it is emitted as <c>langfuse.release</c> on every exported span so scores, cost,
    /// and latency can be compared across releases. <see langword="null"/> by default.
    /// </summary>
    public string? Release { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether Needlr's <c>gen_ai</c> metrics (including the
    /// <c>gen_ai.client.token.usage</c> histogram) are exported alongside traces. Defaults to
    /// <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// As of Langfuse v3.x the OTLP metrics endpoint (<c>/api/public/otel/v1/metrics</c>) accepts
    /// requests (returns HTTP 200) but does <strong>not</strong> ingest the data — there is no
    /// corresponding metrics read API, so exported metrics are silently discarded. Token usage is
    /// already carried on the generation spans, so metrics export is off by default. Enable this
    /// only if you point the exporter at a backend that ingests OTLP metrics.
    /// </remarks>
    public bool IncludeMetrics { get; set; }

    /// <summary>
    /// Gets or sets how a failed evaluation-score upload is handled. Defaults to
    /// <see cref="LangfuseScoreFailureMode.NonFatal"/> so a transient Langfuse outage does not turn
    /// a passing eval into a failure.
    /// </summary>
    public LangfuseScoreFailureMode ScoreFailureMode { get; set; } = LangfuseScoreFailureMode.NonFatal;

    /// <summary>
    /// Gets the bounded timeout and retry settings used for Langfuse REST API calls.
    /// </summary>
    public LangfuseHttpOptions Http { get; } = new();

    /// <summary>
    /// Gets the bounded local queue and OTLP trace-export settings.
    /// </summary>
    public LangfuseTraceExportOptions TraceExport { get; } = new();

    /// <summary>
    /// Gets or sets the resource-lock provider used by standalone clients while ensuring score
    /// configs and custom models. Defaults to in-process coordination.
    /// </summary>
    /// <remarks>
    /// Hosted applications can register an <see cref="ILangfuseResourceLockProvider"/> before
    /// calling <c>AddNeedlrLangfuse</c>; that dependency-injection registration takes precedence.
    /// Use a distributed implementation when multiple processes initialize the same project.
    /// </remarks>
    public ILangfuseResourceLockProvider ResourceLockProvider { get; set; } =
        new LangfuseInProcessResourceLockProvider();

    /// <summary>
    /// Gets or sets an optional callback invoked when a score upload fails under
    /// <see cref="LangfuseScoreFailureMode.NonFatal"/>. Use it to log the loss with your own logger.
    /// </summary>
    public Action<LangfuseScoreError>? ScoreErrorCallback { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether evaluator metric names are normalised to
    /// <c>snake_case</c> before being sent as Langfuse score names. Off by default; names are sent
    /// verbatim. Enable for consistent dashboard filtering/grouping.
    /// </summary>
    public bool NormalizeScoreNames { get; set; }

    /// <summary>
    /// Gets or sets an optional callback for library diagnostic messages — for example, the warning
    /// emitted when credentials are present but no export target (<see cref="Host"/> or
    /// <see cref="Region"/>) was chosen. Wire it to your logger to surface these conditions.
    /// </summary>
    public Action<string>? DiagnosticsCallback { get; set; }

    /// <summary>
    /// Gets or sets the head-based trace sampling ratio in the range <c>0.0</c> to <c>1.0</c>.
    /// Defaults to <c>1.0</c> (sample everything), which is appropriate for eval workloads.
    /// </summary>
    public double SamplingRatio { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the total timeout budget used by <see cref="System.IDisposable.Dispose"/> for
    /// final local trace and metric provider shutdown. Defaults to five seconds.
    /// </summary>
    /// <remarks>
    /// Set this to <see cref="System.Threading.Timeout.InfiniteTimeSpan"/> only when an unbounded
    /// disposal wait is explicitly desired. Other negative values are invalid for an enabled
    /// standalone session.
    /// </remarks>
    public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the name of Needlr's agent <see cref="System.Diagnostics.ActivitySource"/>
    /// to export. Defaults to <see cref="AgentFrameworkMetricsOptions.MeterName"/>'s default.
    /// </summary>
    public string AgentActivitySourceName { get; set; } = MetricsDefaults.MeterName;

    /// <summary>
    /// Gets or sets the name of Needlr's agent <see cref="System.Diagnostics.Metrics.Meter"/>
    /// to export. Defaults to <see cref="AgentFrameworkMetricsOptions.MeterName"/>'s default.
    /// </summary>
    public string AgentMeterName { get; set; } = MetricsDefaults.MeterName;

    /// <summary>
    /// Gets or sets the name of the meter that owns the <c>gen_ai.client.token.usage</c>
    /// histogram shared by Needlr and MEAI. Defaults to
    /// <see cref="AgentFrameworkMetricsOptions.GenAiMeterName"/>'s default
    /// (<c>"Experimental.Microsoft.Extensions.AI"</c>).
    /// </summary>
    public string GenAiMeterName { get; set; } = MetricsDefaults.GenAiMeterName;

    /// <summary>
    /// Gets a mutable list of additional <see cref="System.Diagnostics.ActivitySource"/> names
    /// to export — for example a host's own source or MAF's agent source.
    /// </summary>
    public IList<string> AdditionalActivitySources { get; } = [];

    /// <summary>
    /// Gets a mutable list of additional <see cref="System.Diagnostics.Metrics.Meter"/> names
    /// to export.
    /// </summary>
    public IList<string> AdditionalMeters { get; } = [];

    /// <summary>
    /// Gets a value indicating whether both API keys are present and export is enabled. Does not
    /// account for whether an export target was chosen — see <see cref="HasExplicitTarget"/> and
    /// <see cref="IsConfigured"/>.
    /// </summary>
    public bool HasCredentials =>
        Enabled
        && !string.IsNullOrWhiteSpace(PublicKey)
        && !string.IsNullOrWhiteSpace(SecretKey);

    /// <summary>
    /// Gets a value indicating whether an explicit export target was chosen — either a
    /// <see cref="Host"/> (self-hosted) or a <see cref="Region"/> (deliberate Langfuse Cloud
    /// opt-in).
    /// </summary>
    public bool HasExplicitTarget =>
        !string.IsNullOrWhiteSpace(Host) || Region.HasValue;

    /// <summary>
    /// Gets a value indicating whether the integration is fully configured to export: credentials
    /// are present, export is enabled, and an explicit target (<see cref="Host"/> or
    /// <see cref="Region"/>) was chosen. Requiring an explicit target prevents accidentally sending
    /// traces to Langfuse Cloud.
    /// </summary>
    public bool IsConfigured => HasCredentials && HasExplicitTarget;

    /// <summary>
    /// Builds a <see cref="LangfuseOptions"/> from the standard Langfuse environment variables
    /// (<see cref="PublicKeyEnvironmentVariable"/>, <see cref="SecretKeyEnvironmentVariable"/>,
    /// and <see cref="HostEnvironmentVariable"/>).
    /// </summary>
    /// <returns>
    /// A populated <see cref="LangfuseOptions"/>. When the keys are absent the result has
    /// <see cref="IsConfigured"/> equal to <see langword="false"/> and the integration no-ops.
    /// </returns>
    public static LangfuseOptions FromEnvironment()
    {
        var options = new LangfuseOptions
        {
            PublicKey = NullIfBlank(System.Environment.GetEnvironmentVariable(PublicKeyEnvironmentVariable)),
            SecretKey = NullIfBlank(System.Environment.GetEnvironmentVariable(SecretKeyEnvironmentVariable)),
            Host = NullIfBlank(System.Environment.GetEnvironmentVariable(HostEnvironmentVariable)),
        };

        return options;
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
