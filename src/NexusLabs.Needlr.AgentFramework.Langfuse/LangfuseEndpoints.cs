using System.Text;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Resolves the Langfuse OTLP/HTTP ingestion endpoints and authentication headers from a
/// <see cref="LangfuseOptions"/> instance.
/// </summary>
/// <remarks>
/// Langfuse exposes an OpenTelemetry-compatible endpoint at <c>/api/public/otel</c> and
/// authenticates with HTTP Basic auth over the base64 of <c>publicKey:secretKey</c>. Only
/// OTLP over HTTP is supported (gRPC is not), so the .NET exporter is always configured for
/// the <c>HttpProtobuf</c> protocol against the signal-specific paths produced here.
/// </remarks>
internal sealed class LangfuseEndpoints
{
    private const string OtelBasePath = "api/public/otel";

    private LangfuseEndpoints(
        Uri baseUrl,
        Uri tracesEndpoint,
        Uri metricsEndpoint,
        Uri scoresEndpoint,
        string authorizationHeaderValue,
        string headers)
    {
        BaseUrl = baseUrl;
        TracesEndpoint = tracesEndpoint;
        MetricsEndpoint = metricsEndpoint;
        ScoresEndpoint = scoresEndpoint;
        AuthorizationHeaderValue = authorizationHeaderValue;
        Headers = headers;
    }

    /// <summary>
    /// Gets the resolved Langfuse base URL with a trailing slash (for example
    /// <c>https://cloud.langfuse.com/</c>). Public REST API paths such as
    /// <c>api/public/dataset-run-items</c> are resolved relative to this.
    /// </summary>
    public Uri BaseUrl { get; }

    /// <summary>Gets the full OTLP/HTTP traces endpoint (<c>.../api/public/otel/v1/traces</c>).</summary>
    public Uri TracesEndpoint { get; }

    /// <summary>Gets the full OTLP/HTTP metrics endpoint (<c>.../api/public/otel/v1/metrics</c>).</summary>
    public Uri MetricsEndpoint { get; }

    /// <summary>Gets the Langfuse public Scores API endpoint (<c>.../api/public/scores</c>).</summary>
    public Uri ScoresEndpoint { get; }

    /// <summary>Gets the <c>Authorization</c> header value (<c>Basic &lt;base64&gt;</c>).</summary>
    public string AuthorizationHeaderValue { get; }

    /// <summary>
    /// Gets the comma-separated header string consumed by the OTLP exporter, carrying the
    /// <c>Authorization</c> header and the Langfuse ingestion-version header.
    /// </summary>
    public string Headers { get; }

    /// <summary>
    /// Resolves the ingestion endpoints and headers for the supplied options.
    /// </summary>
    /// <param name="options">A configured options instance.</param>
    /// <returns>The resolved <see cref="LangfuseEndpoints"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="options"/> is missing the public or secret key, or its
    /// <see cref="LangfuseOptions.Host"/>/<see cref="LangfuseOptions.Region"/> cannot be resolved
    /// to an absolute HTTP(S) URL.
    /// </exception>
    public static LangfuseEndpoints Resolve(LangfuseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.PublicKey) || string.IsNullOrWhiteSpace(options.SecretKey))
        {
            throw new InvalidOperationException(
                "Langfuse public and secret keys are required to resolve ingestion endpoints. " +
                "Check LangfuseOptions.IsConfigured before resolving endpoints.");
        }

        var baseUrl = ResolveBaseUrl(options);

        var auth = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{options.PublicKey}:{options.SecretKey}"));
        var authorizationHeaderValue = $"Basic {auth}";
        var headers = $"Authorization={authorizationHeaderValue},x-langfuse-ingestion-version=4";

        return new LangfuseEndpoints(
            baseUrl: baseUrl,
            tracesEndpoint: new Uri(baseUrl, $"{OtelBasePath}/v1/traces"),
            metricsEndpoint: new Uri(baseUrl, $"{OtelBasePath}/v1/metrics"),
            scoresEndpoint: new Uri(baseUrl, "api/public/scores"),
            authorizationHeaderValue: authorizationHeaderValue,
            headers: headers);
    }

    private static Uri ResolveBaseUrl(LangfuseOptions options)
    {
        string raw;
        if (!string.IsNullOrWhiteSpace(options.Host))
        {
            raw = options.Host;
        }
        else if (options.Region is { } region)
        {
            raw = RegionBaseUrl(region);
        }
        else
        {
            throw new InvalidOperationException(
                "Langfuse export target is not set. Provide a Host (self-hosted) or a Region " +
                "(Langfuse Cloud). Check LangfuseOptions.IsConfigured before resolving endpoints.");
        }

        if (!Uri.TryCreate(EnsureTrailingSlash(raw), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                $"Langfuse host '{raw}' is not a valid absolute HTTP(S) URL.");
        }

        return uri;
    }

    private static string RegionBaseUrl(LangfuseRegion region) => region switch
    {
        LangfuseRegion.Eu => "https://cloud.langfuse.com",
        LangfuseRegion.Us => "https://us.cloud.langfuse.com",
        LangfuseRegion.Jp => "https://jp.cloud.langfuse.com",
        LangfuseRegion.Hipaa => "https://hipaa.cloud.langfuse.com",
        _ => throw new InvalidOperationException($"Unknown Langfuse region '{region}'."),
    };

    private static string EnsureTrailingSlash(string url) =>
        url.EndsWith('/') ? url : url + "/";
}
