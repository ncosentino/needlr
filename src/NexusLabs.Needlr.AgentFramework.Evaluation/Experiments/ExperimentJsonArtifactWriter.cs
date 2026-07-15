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
[DoNotAutoRegister]
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
        writer.WritePropertyName("runEvaluations");
        writer.WriteStartArray();
        foreach (var runEvaluation in result.RunEvaluations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteRunEvaluation(writer, runEvaluation);
        }

        writer.WriteEndArray();
        writer.WritePropertyName("policyResults");
        writer.WriteStartArray();
        foreach (var policy in result.PolicyResults)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WritePolicy(writer, policy);
        }

        writer.WriteEndArray();
        writer.WriteString("decision", ToJson(result.Decision));
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
        writer.WritePropertyName("correlations");
        WriteCorrelations(writer, item.Correlations);
        writer.WritePropertyName("publications");
        writer.WriteStartArray();
        foreach (var publication in item.Publications)
        {
            WritePublication(writer, publication);
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
        if (attempt.DelayBeforeNextAttempt is { } retryDelay)
        {
            writer.WriteNumber(
                "delayBeforeNextAttemptMilliseconds",
                retryDelay.TotalMilliseconds);
        }
        else
        {
            writer.WriteNull("delayBeforeNextAttemptMilliseconds");
        }

        writer.WritePropertyName("failure");
        WriteFailure(writer, attempt.Failure);
        writer.WriteEndObject();
    }

    private static void WritePublication(
        Utf8JsonWriter writer,
        ExperimentItemPublicationResult publication)
    {
        writer.WriteStartObject();
        writer.WriteString("name", publication.Name);
        writer.WriteBoolean("isRequired", publication.IsRequired);
        writer.WriteString("status", ToJson(publication.Status));
        writer.WritePropertyName("correlations");
        WriteCorrelations(writer, publication.Correlations);
        writer.WritePropertyName("failure");
        WriteFailure(writer, publication.Failure);
        writer.WriteEndObject();
    }

    private static void WriteCorrelations(
        Utf8JsonWriter writer,
        IReadOnlyList<ExperimentItemCorrelation> correlations)
    {
        writer.WriteStartArray();
        foreach (var correlation in correlations)
        {
            writer.WriteStartObject();
            writer.WriteString("namespace", correlation.Namespace);
            writer.WriteString("name", correlation.Name);
            writer.WriteString("value", correlation.Value);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteRunEvaluation(
        Utf8JsonWriter writer,
        ExperimentRunEvaluationResult evaluation)
    {
        writer.WriteStartObject();
        writer.WriteString("name", evaluation.Name);
        writer.WriteString("status", ToJson(evaluation.Status));
        writer.WritePropertyName("metrics");
        writer.WriteStartArray();
        foreach (var metric in evaluation.Metrics)
        {
            WriteMetric(writer, metric);
        }

        writer.WriteEndArray();
        writer.WritePropertyName("failure");
        WriteFailure(writer, evaluation.Failure);
        writer.WriteEndObject();
    }

    private static void WritePolicy(
        Utf8JsonWriter writer,
        ExperimentPolicyResult policy)
    {
        writer.WriteStartObject();
        writer.WriteString("name", policy.Name);
        writer.WriteString("kind", ToJson(policy.Kind));
        writer.WriteBoolean("isRequired", policy.IsRequired);
        writer.WriteString("decision", ToJson(policy.Decision));
        writer.WritePropertyName("deterministicEvidence");
        WriteDeterministicEvidence(writer, policy.DeterministicEvidence);
        writer.WritePropertyName("statisticalEvidence");
        WriteStatisticalEvidence(writer, policy.StatisticalEvidence);
        writer.WritePropertyName("failure");
        WriteFailure(writer, policy.Failure);
        writer.WriteEndObject();
    }

    private static void WriteDeterministicEvidence(
        Utf8JsonWriter writer,
        ExperimentDeterministicPolicyEvidence? evidence)
    {
        if (evidence is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        writer.WriteString("runEvaluationName", evidence.RunEvaluationName);
        writer.WritePropertyName("thresholds");
        WriteThresholdResult(writer, evidence.Thresholds);
        writer.WriteString("unavailableReason", evidence.UnavailableReason);
        writer.WriteEndObject();
    }

    private static void WriteThresholdResult(
        Utf8JsonWriter writer,
        EvaluationThresholdResult? result)
    {
        if (result is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        writer.WriteString("decision", ToJson(result.Decision));
        writer.WritePropertyName("outcomes");
        writer.WriteStartArray();
        foreach (var outcome in result.Outcomes)
        {
            writer.WriteStartObject();
            writer.WriteString("metricName", outcome.MetricName);
            writer.WriteString("kind", ToJson(outcome.Kind));
            writer.WriteString("status", ToJson(outcome.Status));
            writer.WriteBoolean("isRequired", outcome.IsRequired);
            WriteNullableNumber(writer, "numericThreshold", outcome.NumericThreshold);
            if (outcome.BooleanExpected is { } booleanExpected)
            {
                writer.WriteBoolean("booleanExpected", booleanExpected);
            }
            else
            {
                writer.WriteNull("booleanExpected");
            }

            WriteNullableNumber(writer, "numericValue", outcome.NumericValue);
            if (outcome.BooleanValue is { } booleanValue)
            {
                writer.WriteBoolean("booleanValue", booleanValue);
            }
            else
            {
                writer.WriteNull("booleanValue");
            }

            writer.WriteString("message", outcome.Message);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteStatisticalEvidence(
        Utf8JsonWriter writer,
        ExperimentBinaryStatisticalEvidence? evidence)
    {
        if (evidence is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        writer.WriteString("metricName", evidence.MetricName);
        writer.WriteNumber("totalTrialCount", evidence.TotalTrialCount);
        writer.WriteNumber("attemptCount", evidence.AttemptCount);
        writer.WriteNumber("sampleCount", evidence.SampleCount);
        writer.WriteNumber("successCount", evidence.SuccessCount);
        writer.WriteNumber("failureCount", evidence.FailureCount);
        writer.WriteNumber("executionFailureCount", evidence.ExecutionFailureCount);
        writer.WriteNumber("exclusionCount", evidence.ExclusionCount);
        writer.WritePropertyName("statusCounts");
        writer.WriteStartArray();
        foreach (var statusCount in evidence.StatusCounts)
        {
            writer.WriteStartObject();
            writer.WriteString("status", ToJson(statusCount.Status));
            writer.WriteNumber("count", statusCount.Count);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        WriteNullableNumber(writer, "estimate", evidence.Estimate);
        WriteNullableNumber(
            writer,
            "oneSidedLowerBound",
            evidence.OneSidedLowerBound);
        WriteNullableNumber(
            writer,
            "oneSidedUpperBound",
            evidence.OneSidedUpperBound);
        writer.WriteNumber("confidenceLevel", evidence.ConfidenceLevel);
        writer.WriteNumber("requiredSuccessRate", evidence.RequiredSuccessRate);
        writer.WriteNumber("minimumSampleCount", evidence.MinimumSampleCount);
        writer.WriteString("intervalMethod", ToJson(evidence.IntervalMethod));
        writer.WriteString(
            "unknownSampleTreatment",
            ToJson(evidence.UnknownSampleTreatment));
        writer.WriteEndObject();
    }

    private static void WriteNullableNumber(
        Utf8JsonWriter writer,
        string propertyName,
        double? value)
    {
        if (value is { } number)
        {
            writer.WriteNumber(propertyName, number);
        }
        else
        {
            writer.WriteNull(propertyName);
        }
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
        ExperimentItemStatus.PrerequisiteFailed => "prerequisiteFailed",
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
        ExperimentFailureCode.RetryPolicyFailed => "retryPolicyFailed",
        ExperimentFailureCode.RunEvaluationFailed => "runEvaluationFailed",
        ExperimentFailureCode.PolicyFailed => "policyFailed",
        ExperimentFailureCode.ItemScopeFailed => "itemScopeFailed",
        ExperimentFailureCode.ItemScopePrerequisiteFailed => "itemScopePrerequisiteFailed",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "The experiment failure code is not defined."),
    };

    private static string ToJson(ExperimentFailureStage value) => value switch
    {
        ExperimentFailureStage.Execution => "execution",
        ExperimentFailureStage.ItemEvaluation => "itemEvaluation",
        ExperimentFailureStage.RunEvaluation => "runEvaluation",
        ExperimentFailureStage.Policy => "policy",
        ExperimentFailureStage.Publication => "publication",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "The experiment failure stage is not defined."),
    };

    private static string ToJson(ExperimentItemPublicationStatus value) => value switch
    {
        ExperimentItemPublicationStatus.Succeeded => "succeeded",
        ExperimentItemPublicationStatus.Failed => "failed",
        ExperimentItemPublicationStatus.NotAttempted => "notAttempted",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "The item publication status is not defined."),
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

    private static string ToJson(ExperimentRunEvaluationStatus value) => value switch
    {
        ExperimentRunEvaluationStatus.Succeeded => "succeeded",
        ExperimentRunEvaluationStatus.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "The run evaluation status is not defined."),
    };

    private static string ToJson(ExperimentPolicyKind value) => value switch
    {
        ExperimentPolicyKind.Deterministic => "deterministic",
        ExperimentPolicyKind.Statistical => "statistical",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "The experiment policy kind is not defined."),
    };

    private static string ToJson(EvaluationDecision value) => value switch
    {
        EvaluationDecision.Passed => "passed",
        EvaluationDecision.Failed => "failed",
        EvaluationDecision.Inconclusive => "inconclusive",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "The evaluation decision is not defined."),
    };

    private static string ToJson(ExperimentRunDecision value) => value switch
    {
        ExperimentRunDecision.Passed => "passed",
        ExperimentRunDecision.Failed => "failed",
        ExperimentRunDecision.Inconclusive => "inconclusive",
        ExperimentRunDecision.NotEvaluated => "notEvaluated",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "The experiment run decision is not defined."),
    };

    private static string ToJson(EvaluationThresholdKind value) => value switch
    {
        EvaluationThresholdKind.NumericMaximum => "numericMaximum",
        EvaluationThresholdKind.NumericMinimum => "numericMinimum",
        EvaluationThresholdKind.Boolean => "boolean",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "The evaluation threshold kind is not defined."),
    };

    private static string ToJson(EvaluationThresholdStatus value) => value switch
    {
        EvaluationThresholdStatus.Passed => "passed",
        EvaluationThresholdStatus.Failed => "failed",
        EvaluationThresholdStatus.Missing => "missing",
        EvaluationThresholdStatus.Invalid => "invalid",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "The evaluation threshold status is not defined."),
    };

    private static string ToJson(ExperimentConfidenceIntervalMethod value) => value switch
    {
        ExperimentConfidenceIntervalMethod.WilsonScore => "wilsonScore",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "The confidence interval method is not defined."),
    };

    private static string ToJson(ExperimentUnknownSampleTreatment value) => value switch
    {
        ExperimentUnknownSampleTreatment.Inconclusive => "inconclusive",
        ExperimentUnknownSampleTreatment.CountAsFailure => "countAsFailure",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "The unknown sample treatment is not defined."),
    };
}
