using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Writes the schema-versioned deterministic Needlr experiment JSON envelope.
/// </summary>
/// <remarks>
/// Needlr-owned properties and collections use fixed ordering. Caller-owned case and output
/// payload schemas remain caller responsibility. This writer does not claim RFC 8785
/// cryptographic canonicalization.
/// </remarks>
public sealed class ExperimentJsonArtifactWriter
{
    private readonly JsonWriterOptions _writerOptions;

    /// <summary>
    /// Initializes an experiment artifact writer.
    /// </summary>
    /// <param name="writeIndented">Whether to indent the JSON output.</param>
    public ExperimentJsonArtifactWriter(bool writeIndented = true)
    {
        _writerOptions = new JsonWriterOptions
        {
            Indented = writeIndented,
        };
    }

    /// <summary>
    /// Serializes an experiment result using reflection-based caller payload serialization.
    /// </summary>
    /// <typeparam name="TCase">The caller-owned case value type.</typeparam>
    /// <typeparam name="TOutput">The caller-owned output type.</typeparam>
    /// <param name="result">The canonical experiment result.</param>
    /// <param name="payloadSerializerOptions">
    /// Optional serializer options applied only to caller-owned case and output values.
    /// </param>
    /// <returns>The JSON artifact.</returns>
    [RequiresDynamicCode(
        "Reflection-based case and output serialization may require runtime code generation. Use the JsonTypeInfo overload for Native AOT.")]
    [RequiresUnreferencedCode(
        "Reflection-based case and output serialization may require unreferenced members. Use the JsonTypeInfo overload for trimmed applications.")]
    public string Serialize<TCase, TOutput>(
        ExperimentRunResult<TCase, TOutput> result,
        JsonSerializerOptions? payloadSerializerOptions = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        payloadSerializerOptions ??= JsonSerializerOptions.Default;

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, _writerOptions))
        {
            Write(
                writer,
                result,
                (jsonWriter, value) =>
                    JsonSerializer.Serialize(
                        jsonWriter,
                        value,
                        payloadSerializerOptions),
                (jsonWriter, value) =>
                    JsonSerializer.Serialize(
                        jsonWriter,
                        value,
                        payloadSerializerOptions),
                CancellationToken.None);
            writer.Flush();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// Serializes an experiment result using caller-provided serialization metadata for AOT-safe
    /// case and output payloads.
    /// </summary>
    /// <typeparam name="TCase">The caller-owned case value type.</typeparam>
    /// <typeparam name="TOutput">The caller-owned output type.</typeparam>
    /// <param name="result">The canonical experiment result.</param>
    /// <param name="caseTypeInfo">Serialization metadata for <typeparamref name="TCase"/>.</param>
    /// <param name="outputTypeInfo">Serialization metadata for <typeparamref name="TOutput"/>.</param>
    /// <returns>The JSON artifact.</returns>
    public string Serialize<TCase, TOutput>(
        ExperimentRunResult<TCase, TOutput> result,
        JsonTypeInfo<TCase> caseTypeInfo,
        JsonTypeInfo<TOutput> outputTypeInfo)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(caseTypeInfo);
        ArgumentNullException.ThrowIfNull(outputTypeInfo);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, _writerOptions))
        {
            Write(
                writer,
                result,
                (jsonWriter, value) =>
                    JsonSerializer.Serialize(jsonWriter, value, caseTypeInfo),
                (jsonWriter, value) =>
                    JsonSerializer.Serialize(jsonWriter, value, outputTypeInfo),
                CancellationToken.None);
            writer.Flush();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// Writes an experiment result using reflection-based caller payload serialization.
    /// </summary>
    /// <typeparam name="TCase">The caller-owned case value type.</typeparam>
    /// <typeparam name="TOutput">The caller-owned output type.</typeparam>
    /// <param name="destination">The destination stream, which remains open.</param>
    /// <param name="result">The canonical experiment result.</param>
    /// <param name="payloadSerializerOptions">
    /// Optional serializer options applied only to caller-owned case and output values.
    /// </param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes after the artifact is flushed.</returns>
    [RequiresDynamicCode(
        "Reflection-based case and output serialization may require runtime code generation. Use the JsonTypeInfo overload for Native AOT.")]
    [RequiresUnreferencedCode(
        "Reflection-based case and output serialization may require unreferenced members. Use the JsonTypeInfo overload for trimmed applications.")]
    public async Task WriteAsync<TCase, TOutput>(
        Stream destination,
        ExperimentRunResult<TCase, TOutput> result,
        JsonSerializerOptions? payloadSerializerOptions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(result);
        payloadSerializerOptions ??= JsonSerializerOptions.Default;

        using var writer = new Utf8JsonWriter(destination, _writerOptions);
        Write(
            writer,
            result,
            (jsonWriter, value) =>
                JsonSerializer.Serialize(
                    jsonWriter,
                    value,
                    payloadSerializerOptions),
            (jsonWriter, value) =>
                JsonSerializer.Serialize(
                    jsonWriter,
                    value,
                    payloadSerializerOptions),
            cancellationToken);
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes an experiment result using caller-provided serialization metadata for AOT-safe
    /// case and output payloads.
    /// </summary>
    /// <typeparam name="TCase">The caller-owned case value type.</typeparam>
    /// <typeparam name="TOutput">The caller-owned output type.</typeparam>
    /// <param name="destination">The destination stream, which remains open.</param>
    /// <param name="result">The canonical experiment result.</param>
    /// <param name="caseTypeInfo">Serialization metadata for <typeparamref name="TCase"/>.</param>
    /// <param name="outputTypeInfo">Serialization metadata for <typeparamref name="TOutput"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes after the artifact is flushed.</returns>
    public async Task WriteAsync<TCase, TOutput>(
        Stream destination,
        ExperimentRunResult<TCase, TOutput> result,
        JsonTypeInfo<TCase> caseTypeInfo,
        JsonTypeInfo<TOutput> outputTypeInfo,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(caseTypeInfo);
        ArgumentNullException.ThrowIfNull(outputTypeInfo);

        using var writer = new Utf8JsonWriter(destination, _writerOptions);
        Write(
            writer,
            result,
            (jsonWriter, value) =>
                JsonSerializer.Serialize(jsonWriter, value, caseTypeInfo),
            (jsonWriter, value) =>
                JsonSerializer.Serialize(jsonWriter, value, outputTypeInfo),
            cancellationToken);
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void Write<TCase, TOutput>(
        Utf8JsonWriter writer,
        ExperimentRunResult<TCase, TOutput> result,
        Action<Utf8JsonWriter, TCase> writeCase,
        Action<Utf8JsonWriter, TOutput> writeOutput,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", result.SchemaVersion);
        writer.WriteString("runId", result.RunId);
        writer.WriteString("experimentName", result.ExperimentName);
        WriteSource(writer, result.Source);
        writer.WriteString("startedAt", result.StartedAt);
        writer.WriteNumber("durationMilliseconds", result.Duration.TotalMilliseconds);
        writer.WriteNumber("maxConcurrency", result.MaxConcurrency);
        writer.WriteNumber("workerCount", result.WorkerCount);
        writer.WritePropertyName("items");
        writer.WriteStartArray();
        foreach (var item in result.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteItem(
                writer,
                item,
                writeCase,
                writeOutput,
                cancellationToken);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteSource(
        Utf8JsonWriter writer,
        ExperimentSourceReference source)
    {
        writer.WritePropertyName("source");
        writer.WriteStartObject();
        writer.WriteString("name", source.Name);
        writer.WriteString("id", source.Id);
        writer.WriteString("version", source.Version);
        writer.WriteEndObject();
    }

    private static void WriteItem<TCase, TOutput>(
        Utf8JsonWriter writer,
        ExperimentItemResult<TCase, TOutput> item,
        Action<Utf8JsonWriter, TCase> writeCase,
        Action<Utf8JsonWriter, TOutput> writeOutput,
        CancellationToken cancellationToken)
    {
        writer.WriteStartObject();
        writer.WriteNumber("sequence", item.Sequence);
        writer.WriteString("caseId", item.Case.Id);
        writer.WritePropertyName("case");
        writeCase(writer, item.Case.Value);
        writer.WritePropertyName("tags");
        writer.WriteStartArray();
        foreach (var tag in item.Case.Tags)
        {
            writer.WriteStringValue(tag);
        }

        writer.WriteEndArray();
        writer.WriteNumber("trialIndex", item.TrialIndex);
        writer.WriteString("status", ToJson(item.Status));
        writer.WritePropertyName("attempts");
        writer.WriteStartArray();
        foreach (var attempt in item.Attempts)
        {
            WriteAttempt(writer, attempt);
        }

        writer.WriteEndArray();
        writer.WriteBoolean("hasOutput", item.HasOutput);
        if (item.HasOutput)
        {
            writer.WritePropertyName("output");
            writeOutput(writer, item.Output!);
        }

        writer.WritePropertyName("metrics");
        writer.WriteStartArray();
        foreach (var metric in item.Metrics)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteMetric(writer, metric);
        }

        writer.WriteEndArray();
        writer.WritePropertyName("failure");
        WriteFailure(writer, item.Failure);
        writer.WriteEndObject();
    }

    private static void WriteAttempt(
        Utf8JsonWriter writer,
        ExperimentAttemptResult attempt)
    {
        writer.WriteStartObject();
        writer.WriteNumber("attemptNumber", attempt.AttemptNumber);
        writer.WriteString("status", ToJson(attempt.Status));
        writer.WriteString("startedAt", attempt.StartedAt);
        writer.WriteNumber("durationMilliseconds", attempt.Duration.TotalMilliseconds);
        writer.WritePropertyName("failure");
        WriteFailure(writer, attempt.Failure);
        writer.WriteEndObject();
    }

    private static void WriteMetric(
        Utf8JsonWriter writer,
        ExperimentMetricSnapshot metric)
    {
        writer.WriteStartObject();
        writer.WriteString("name", metric.Name);
        writer.WriteString("kind", ToJson(metric.Kind));
        if (metric.NumericValue is { } numeric)
        {
            writer.WriteNumber("numericValue", numeric);
        }
        else
        {
            writer.WriteNull("numericValue");
        }

        if (metric.NonFiniteNumericValue is { } nonFinite)
        {
            writer.WriteString(
                "nonFiniteNumericValue",
                ToJson(nonFinite));
        }
        else
        {
            writer.WriteNull("nonFiniteNumericValue");
        }

        if (metric.BooleanValue is { } boolean)
        {
            writer.WriteBoolean("booleanValue", boolean);
        }
        else
        {
            writer.WriteNull("booleanValue");
        }

        writer.WriteString("stringValue", metric.StringValue);
        writer.WriteString("reason", metric.Reason);
        writer.WritePropertyName("interpretation");
        WriteInterpretation(writer, metric.Interpretation);
        writer.WriteNumber("contextCount", metric.ContextCount);
        writer.WritePropertyName("diagnostics");
        writer.WriteStartArray();
        foreach (var diagnostic in metric.Diagnostics)
        {
            writer.WriteStartObject();
            writer.WriteString("severity", ToJson(diagnostic.Severity));
            writer.WriteString("message", diagnostic.Message);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WritePropertyName("metadata");
        writer.WriteStartObject();
        foreach (var entry in metric.Metadata)
        {
            writer.WriteString(entry.Key, entry.Value);
        }

        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WriteInterpretation(
        Utf8JsonWriter writer,
        ExperimentMetricInterpretationSnapshot? interpretation)
    {
        if (interpretation is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        writer.WriteString("rating", ToJson(interpretation.Rating));
        writer.WriteBoolean("failed", interpretation.Failed);
        writer.WriteString("reason", interpretation.Reason);
        writer.WriteEndObject();
    }

    private static void WriteFailure(
        Utf8JsonWriter writer,
        ExperimentFailure? failure)
    {
        if (failure is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        writer.WriteString("code", ToJson(failure.Code));
        writer.WriteString("stage", ToJson(failure.Stage));
        writer.WriteString("exceptionType", failure.ExceptionType);
        writer.WriteString("message", failure.Message);
        writer.WriteBoolean("isRetryable", failure.IsRetryable);
        writer.WriteEndObject();
    }

    private static string ToJson(ExperimentItemStatus value) => value switch
    {
        ExperimentItemStatus.Succeeded => "succeeded",
        ExperimentItemStatus.ExecutionFailed => "executionFailed",
        ExperimentItemStatus.TimedOut => "timedOut",
        ExperimentItemStatus.Canceled => "canceled",
        ExperimentItemStatus.EvaluationFailed => "evaluationFailed",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "The experiment item status is not defined."),
    };

    private static string ToJson(ExperimentAttemptStatus value) => value switch
    {
        ExperimentAttemptStatus.Succeeded => "succeeded",
        ExperimentAttemptStatus.Failed => "failed",
        ExperimentAttemptStatus.TimedOut => "timedOut",
        ExperimentAttemptStatus.Canceled => "canceled",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "The experiment attempt status is not defined."),
    };

    private static string ToJson(ExperimentFailureCode value) => value switch
    {
        ExperimentFailureCode.ExecutionFailed => "executionFailed",
        ExperimentFailureCode.AttemptTimedOut => "attemptTimedOut",
        ExperimentFailureCode.TaskCanceled => "taskCanceled",
        ExperimentFailureCode.EvaluationFailed => "evaluationFailed",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "The experiment failure code is not defined."),
    };

    private static string ToJson(ExperimentFailureStage value) => value switch
    {
        ExperimentFailureStage.Execution => "execution",
        ExperimentFailureStage.ItemEvaluation => "itemEvaluation",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "The experiment failure stage is not defined."),
    };

    private static string ToJson(ExperimentMetricKind value) => value switch
    {
        ExperimentMetricKind.Numeric => "numeric",
        ExperimentMetricKind.Boolean => "boolean",
        ExperimentMetricKind.String => "string",
        ExperimentMetricKind.None => "none",
        ExperimentMetricKind.Unknown => "unknown",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "The experiment metric kind is not defined."),
    };

    private static string ToJson(ExperimentMetricRating value) => value switch
    {
        ExperimentMetricRating.Unknown => "unknown",
        ExperimentMetricRating.Inconclusive => "inconclusive",
        ExperimentMetricRating.Unacceptable => "unacceptable",
        ExperimentMetricRating.Poor => "poor",
        ExperimentMetricRating.Average => "average",
        ExperimentMetricRating.Good => "good",
        ExperimentMetricRating.Exceptional => "exceptional",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "The experiment metric rating is not defined."),
    };

    private static string ToJson(ExperimentMetricDiagnosticSeverity value) => value switch
    {
        ExperimentMetricDiagnosticSeverity.Informational => "informational",
        ExperimentMetricDiagnosticSeverity.Warning => "warning",
        ExperimentMetricDiagnosticSeverity.Error => "error",
        ExperimentMetricDiagnosticSeverity.Unknown => "unknown",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "The experiment diagnostic severity is not defined."),
    };

    private static string ToJson(ExperimentMetricNonFiniteValue value) => value switch
    {
        ExperimentMetricNonFiniteValue.NaN => "nan",
        ExperimentMetricNonFiniteValue.PositiveInfinity => "positiveInfinity",
        ExperimentMetricNonFiniteValue.NegativeInfinity => "negativeInfinity",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "The non-finite metric value is not defined."),
    };
}
