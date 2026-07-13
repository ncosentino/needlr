using System.Diagnostics;

using OpenTelemetry;
using OpenTelemetry.Exporter;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Creates the instrumented OTLP trace processor shared by hosted and standalone composition.
/// </summary>
internal static class LangfuseTraceExport
{
    public static BaseProcessor<Activity> CreateProcessor(
        LangfuseEndpoints endpoints,
        LangfuseTraceExportOptions options,
        LangfusePublicationHealth health)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(health);
        options.Validate();

        var exporterOptions = new OtlpExporterOptions
        {
            Endpoint = endpoints.TracesEndpoint,
            Protocol = OtlpExportProtocol.HttpProtobuf,
            Headers = endpoints.Headers,
            TimeoutMilliseconds = options.ExporterTimeoutMilliseconds,
        };
        var exporter = new OtlpTraceExporter(exporterOptions);
        return new LangfuseBatchActivityExportProcessor(
            exporter,
            health,
            options.MaxQueueSize,
            options.ScheduledDelayMilliseconds,
            options.MaxBatchSize);
    }
}
