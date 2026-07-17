using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using Microsoft.Extensions.AI.Evaluation;

using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests.Experiments;

public sealed class ExperimentJsonArtifactWriterTests
{
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Overloads_WriteByteIdenticalArtifactsForIdenticalInputs()
    {
        var outcome = await CreateSimpleOutcomeAsync();
        var defaultWriter = new ExperimentJsonArtifactWriter();
        var explicitIndentedWriter = new ExperimentJsonArtifactWriter(writeIndented: true);
        var serializerOptions = new JsonSerializerOptions
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };
        var caseTypeInfo = (JsonTypeInfo<int>)serializerOptions.GetTypeInfo(typeof(int));
        var outputTypeInfo = (JsonTypeInfo<string>)serializerOptions.GetTypeInfo(typeof(string));

        var tokenlessSerialization = defaultWriter.Serialize(outcome);
        var explicitOptionsSerialization = defaultWriter.Serialize(
            outcome,
            JsonSerializerOptions.Default);
        var explicitConstructorSerialization = explicitIndentedWriter.Serialize(outcome);
        Assert.Equal(tokenlessSerialization, explicitOptionsSerialization);
        Assert.Equal(tokenlessSerialization, explicitConstructorSerialization);

        using var tokenlessReflectionStream = new MemoryStream();
#pragma warning disable xUnit1051 // This test intentionally exercises the tokenless reflection-write overload.
        await defaultWriter.WriteAsync(tokenlessReflectionStream, outcome);
#pragma warning restore xUnit1051
        using var cancellableReflectionStream = new MemoryStream();
        await defaultWriter.WriteAsync(
            cancellableReflectionStream,
            outcome,
            _cancellationToken);
        Assert.Equal(
            tokenlessReflectionStream.ToArray(),
            cancellableReflectionStream.ToArray());

        using var explicitOptionsStream = new MemoryStream();
        await defaultWriter.WriteAsync(
            explicitOptionsStream,
            outcome,
            JsonSerializerOptions.Default,
            _cancellationToken);
        using var nullOptionsStream = new MemoryStream();
        await defaultWriter.WriteAsync(
            nullOptionsStream,
            outcome,
            null,
            _cancellationToken);
        Assert.Equal(
            tokenlessSerialization,
            System.Text.Encoding.UTF8.GetString(explicitOptionsStream.ToArray()));
        Assert.Equal(
            explicitOptionsStream.ToArray(),
            nullOptionsStream.ToArray());

        using var aotStream = new MemoryStream();
        await defaultWriter.WriteAsync(
            aotStream,
            outcome,
            caseTypeInfo,
            outputTypeInfo,
            _cancellationToken);
        Assert.Equal(
            defaultWriter.Serialize(outcome, caseTypeInfo, outputTypeInfo),
            System.Text.Encoding.UTF8.GetString(aotStream.ToArray()));
    }

    [Fact]
    public async Task WriteAsync_CanceledTokenPreservesTokenAndWritesNoBytes()
    {
        var outcome = await CreateSimpleOutcomeAsync();
        var writer = new ExperimentJsonArtifactWriter(writeIndented: false);
        var serializerOptions = new JsonSerializerOptions
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };
        var caseTypeInfo = (JsonTypeInfo<int>)serializerOptions.GetTypeInfo(typeof(int));
        var outputTypeInfo = (JsonTypeInfo<string>)serializerOptions.GetTypeInfo(typeof(string));
        using var cancellation =
            CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken);
        cancellation.Cancel();
        using var reflectionStream = new MemoryStream();

        var reflectionException = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            writer.WriteAsync(
                reflectionStream,
                outcome,
                JsonSerializerOptions.Default,
                cancellation.Token));

        Assert.Equal(cancellation.Token, reflectionException.CancellationToken);
        Assert.Equal(0, reflectionStream.Length);

        using var aotStream = new MemoryStream();
        var aotException = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            writer.WriteAsync(
                aotStream,
                outcome,
                caseTypeInfo,
                outputTypeInfo,
                cancellation.Token));

        Assert.Equal(cancellation.Token, aotException.CancellationToken);
        Assert.Equal(0, aotStream.Length);
    }

    [Fact]
    public async Task Serialize_WritesStableEnvelopeOrderingAndNormalizedMetrics()
    {
        var evaluation = new EvaluationResult(
            new NumericMetric("z_finite", 0.5, "finite"),
            new BooleanMetric("m_boolean", true),
            new NumericMetric("a_nan", double.NaN, "not finite"),
            new EvaluationMetric("b_none", "no typed value"));
        var definition = new ExperimentDefinition<int, string>
        {
            Name = "artifact",
            CaseSource = new LocalExperimentCaseSource<int>(
                "local",
                [
                    new ExperimentCase<int> { Id = "success", Value = 7 },
                    new ExperimentCase<int> { Id = "failure", Value = 8 },
                ]),
            Task = (context, _) => context.Case.Id == "failure"
                ? throw new InvalidOperationException("boom")
                : ValueTask.FromResult("output"),
            ItemEvaluator = (_, _) => ValueTask.FromResult(evaluation),
            ItemScopes =
            [
                new CallbackExperimentItemScopeProvider<int, string>(
                    "artifact-scope",
                    isRequired: false,
                    ExperimentItemScopeFailureMode.BestEffort,
                    (_, _) =>
                    {
                        IExperimentItemScope<int, string> scope =
                            new CallbackExperimentItemScope<int, string>(
                                new Dictionary<Type, object>(),
                                () => null,
                                (item, _) => ValueTask.FromResult(
                                    ExperimentItemPublicationOperationResult.Succeeded(
                                        [
                                            new ExperimentItemCorrelation
                                            {
                                                Namespace = "artifact",
                                                Name = "case",
                                                Value = item.Case.Id,
                                            },
                                        ])),
                                _ => ValueTask.CompletedTask,
                                () => ValueTask.CompletedTask);
                        return ValueTask.FromResult(scope);
                    }),
            ],
        };
        var result = await new ExperimentRunner().RunAsync(
            definition,
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 2 },
            _cancellationToken);
        var writer = new ExperimentJsonArtifactWriter(writeIndented: false);

        var serializerOptions = new JsonSerializerOptions
        {
            NumberHandling = JsonNumberHandling.WriteAsString,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };
        var caseTypeInfo = (JsonTypeInfo<int>)serializerOptions.GetTypeInfo(typeof(int));
        var outputTypeInfo = (JsonTypeInfo<string>)serializerOptions.GetTypeInfo(typeof(string));
        var json = writer.Serialize(result, caseTypeInfo, outputTypeInfo);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal(
            [
                "schemaVersion",
                "result",
                "publicationStatus",
                "sinkResults",
            ],
            root.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.Equal(4, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("succeeded", root.GetProperty("publicationStatus").GetString());
        Assert.Empty(root.GetProperty("sinkResults").EnumerateArray());
        var resultRoot = root.GetProperty("result");
        Assert.Equal(3, resultRoot.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("run-1", resultRoot.GetProperty("runId").GetString());
        Assert.Equal("notEvaluated", resultRoot.GetProperty("decision").GetString());
        Assert.Empty(resultRoot.GetProperty("runEvaluations").EnumerateArray());
        Assert.Empty(resultRoot.GetProperty("policyResults").EnumerateArray());

        var items = resultRoot.GetProperty("items");
        Assert.Equal(2, items.GetArrayLength());
        var success = items[0];
        Assert.Equal("succeeded", success.GetProperty("status").GetString());
        Assert.Equal("7", success.GetProperty("case").GetString());
        Assert.Equal("output", success.GetProperty("output").GetString());
        Assert.False(success.TryGetProperty("evaluation", out _));
        Assert.Equal(
            [
                "sequence",
                "caseId",
                "case",
                "tags",
                "trialIndex",
                "status",
                "attempts",
                "hasOutput",
                "output",
                "metrics",
                "correlations",
                "publications",
                "failure",
            ],
            success.EnumerateObject().Select(property => property.Name));
        var metrics = success.GetProperty("metrics");
        Assert.Equal(
            new string?[] { "a_nan", "b_none", "m_boolean", "z_finite" },
            metrics.EnumerateArray()
                .Select(metric => metric.GetProperty("name").GetString())
                .ToArray());
        Assert.Equal(
            "nan",
            metrics[0].GetProperty("nonFiniteNumericValue").GetString());
        Assert.Equal("numeric", metrics[0].GetProperty("kind").GetString());
        Assert.Equal("none", metrics[1].GetProperty("kind").GetString());
        var correlation = Assert.Single(success.GetProperty("correlations").EnumerateArray());
        Assert.Equal("artifact", correlation.GetProperty("namespace").GetString());
        Assert.Equal("case", correlation.GetProperty("name").GetString());
        Assert.Equal("success", correlation.GetProperty("value").GetString());
        var publication = Assert.Single(success.GetProperty("publications").EnumerateArray());
        Assert.Equal(
            ["name", "isRequired", "status", "correlations", "failure"],
            publication.EnumerateObject().Select(property => property.Name));
        Assert.Equal("artifact-scope", publication.GetProperty("name").GetString());
        Assert.False(
            publication.GetProperty("isRequired").GetBoolean(),
            "Expected the optional publication requirement to be serialized.");
        Assert.Equal("succeeded", publication.GetProperty("status").GetString());

        var failure = items[1];
        Assert.Equal("executionFailed", failure.GetProperty("status").GetString());
        var failurePayload = failure.GetProperty("failure");
        Assert.Equal("executionFailed", failurePayload.GetProperty("code").GetString());
        Assert.Equal("execution", failurePayload.GetProperty("stage").GetString());
        Assert.Equal(typeof(InvalidOperationException).FullName, failurePayload.GetProperty("exceptionType").GetString());
        Assert.Equal("boom", failurePayload.GetProperty("message").GetString());
        Assert.False(
            failurePayload.GetProperty("isRetryable").GetBoolean(),
            "A terminal failure with no selected retry must not claim retry-policy eligibility.");
        Assert.DoesNotContain("stack", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("$type", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Serialize_NormalizedOperationsPreserveCanonicalPublicationJsonBytes()
    {
        var scope = new CallbackExperimentItemScopeProvider<int, string>(
            "artifact-scope",
            isRequired: true,
            ExperimentItemScopeFailureMode.BestEffort,
            (_, _) =>
            {
                IExperimentItemScope<int, string> itemScope =
                    new CallbackExperimentItemScope<int, string>(
                        new Dictionary<Type, object>(),
                        () => null,
                        (_, _) => ValueTask.FromResult(
                            ExperimentItemPublicationOperationResult.Succeeded(
                                [
                                    new ExperimentItemCorrelation
                                    {
                                        Namespace = "artifact",
                                        Name = "item",
                                        Value = "correlation-1",
                                    },
                                ])),
                        _ => ValueTask.CompletedTask,
                        () => ValueTask.CompletedTask);
                return ValueTask.FromResult(itemScope);
            });
        var sink = new CallbackExperimentResultSink<int, string>(
            "artifact-sink",
            isRequired: false,
            (_, _) => ValueTask.FromResult(
                ExperimentSinkPublicationOperationResult.NotAttempted()));
        var actual = await new ExperimentRunner().RunAsync(
            new ExperimentDefinition<int, string>
            {
                Name = "publication-contract",
                CaseSource = new LocalExperimentCaseSource<int>(
                    "local",
                    [new ExperimentCase<int> { Id = "case-1", Value = 1 }]),
                Task = (_, _) => ValueTask.FromResult("output"),
                ItemScopes = [scope],
                Sinks = [sink],
            },
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
            _cancellationToken);
        var writer = new ExperimentJsonArtifactWriter(writeIndented: false);
        using var document = JsonDocument.Parse(writer.Serialize(actual));
        var item = Assert.Single(
            document.RootElement.GetProperty("result").GetProperty("items").EnumerateArray());
        var publication = Assert.Single(
            item.GetProperty("publications").EnumerateArray());
        var sinkResult = Assert.Single(
            document.RootElement.GetProperty("sinkResults").EnumerateArray());

        Assert.Equal(
            """[{"namespace":"artifact","name":"item","value":"correlation-1"}]""",
            item.GetProperty("correlations").GetRawText());
        Assert.Equal(
            """{"name":"artifact-scope","isRequired":true,"status":"succeeded","correlations":[{"namespace":"artifact","name":"item","value":"correlation-1"}],"failure":null}""",
            publication.GetRawText());
        Assert.Equal(
            """{"name":"artifact-sink","isRequired":false,"status":"notAttempted","failure":null}""",
            sinkResult.GetRawText());
    }

    [Fact]
    public async Task Serialize_WritesRetryRunEvaluationAndPolicyEvidence()
    {
        var executions = 0;
        var definition = new ExperimentDefinition<int, int>
        {
            Name = "phase-2-artifact",
            CaseSource = new LocalExperimentCaseSource<int>(
                "local",
                [new ExperimentCase<int> { Id = "case-1", Value = 7 }]),
            Task = (_, _) => Interlocked.Increment(ref executions) == 1
                ? throw new InvalidOperationException("retry")
                : ValueTask.FromResult(9),
            ItemEvaluator = (_, _) => ValueTask.FromResult(new EvaluationResult(
                new BooleanMetric("passed", true))),
            RunEvaluators =
            [
                new ExperimentRunEvaluator<int, int>(
                    "aggregate",
                    (_, _) => ValueTask.FromResult(new EvaluationResult(
                        new NumericMetric("success_rate", 1)))),
                new ExperimentRunEvaluator<int, int>(
                    "broken",
                    (_, _) => throw new InvalidOperationException("run evaluator failed")),
            ],
            Policies =
            [
                new ExperimentRunEvaluationThresholdPolicy<int, int>(
                    "threshold",
                    "aggregate",
                    new EvaluationThresholdEvaluator()
                        .RequireNumericMin("success_rate", 0.8)),
                new ExperimentBinarySuccessPolicy<int, int>(
                    "binary",
                    "passed",
                    requiredSuccessRate: 0,
                    minimumSampleCount: 1,
                    confidenceLevel: 0.95),
                new ThrowingExperimentPolicy<int, int>("broken-policy"),
            ],
            Sinks =
            [
                new CallbackExperimentResultSink<int, int>(
                    "optional-artifact",
                    isRequired: false,
                    (_, _) => throw new InvalidOperationException("sink failed")),
            ],
        };
        var result = await new ExperimentRunner().RunAsync(
            definition,
            new ExperimentRunOptions
            {
                RunId = "run-2",
                MaxConcurrency = 1,
                RetryPolicy = new ExperimentRetryPolicy(
                    maxAttempts: 2,
                    retryOn: ExperimentRetryableOutcome.ExecutionFailure,
                    delay: TimeSpan.Zero),
            },
            _cancellationToken);
        var writer = new ExperimentJsonArtifactWriter(writeIndented: false);

        var json = writer.Serialize(result);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal("partiallyFailed", root.GetProperty("publicationStatus").GetString());
        var sinkResult = Assert.Single(root.GetProperty("sinkResults").EnumerateArray());
        Assert.Equal(
            ["name", "isRequired", "status", "failure"],
            sinkResult.EnumerateObject().Select(property => property.Name));
        Assert.Equal("optional-artifact", sinkResult.GetProperty("name").GetString());
        Assert.Equal("failed", sinkResult.GetProperty("status").GetString());
        Assert.Equal(
            "resultSinkFailed",
            sinkResult.GetProperty("failure").GetProperty("code").GetString());
        var resultRoot = root.GetProperty("result");
        Assert.Equal("inconclusive", resultRoot.GetProperty("decision").GetString());
        var attempts = resultRoot.GetProperty("items")[0].GetProperty("attempts");
        Assert.Equal(2, attempts.GetArrayLength());
        Assert.Equal(
            0,
            attempts[0].GetProperty("delayBeforeNextAttemptMilliseconds").GetDouble());
        var runEvaluations = resultRoot.GetProperty("runEvaluations");
        Assert.Equal(2, runEvaluations.GetArrayLength());
        Assert.Equal("aggregate", runEvaluations[0].GetProperty("name").GetString());
        Assert.Equal("succeeded", runEvaluations[0].GetProperty("status").GetString());
        Assert.Equal("broken", runEvaluations[1].GetProperty("name").GetString());
        Assert.Equal(
            "runEvaluationFailed",
            runEvaluations[1].GetProperty("failure").GetProperty("code").GetString());
        Assert.Equal(
            "runEvaluation",
            runEvaluations[1].GetProperty("failure").GetProperty("stage").GetString());

        var policies = resultRoot.GetProperty("policyResults");
        Assert.Equal(3, policies.GetArrayLength());
        Assert.Equal("deterministic", policies[0].GetProperty("kind").GetString());
        Assert.Equal(
            "passed",
            policies[0]
                .GetProperty("deterministicEvidence")
                .GetProperty("thresholds")
                .GetProperty("decision")
                .GetString());
        Assert.Equal("statistical", policies[1].GetProperty("kind").GetString());
        var statistics = policies[1].GetProperty("statisticalEvidence");
        Assert.Equal(1, statistics.GetProperty("totalTrialCount").GetInt32());
        Assert.Equal(2, statistics.GetProperty("attemptCount").GetInt32());
        Assert.Equal("wilsonScore", statistics.GetProperty("intervalMethod").GetString());
        Assert.Equal(
            "policyFailed",
            policies[2].GetProperty("failure").GetProperty("code").GetString());
        Assert.Equal(
            "policy",
            policies[2].GetProperty("failure").GetProperty("stage").GetString());
        Assert.DoesNotContain("stack", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("$type", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Serialize_RetryPolicyFailure_WritesStructuredFailure()
    {
        var result = await new ExperimentRunner().RunAsync(
            new ExperimentDefinition<int, int>
            {
                Name = "retry-policy-failure",
                CaseSource = new LocalExperimentCaseSource<int>(
                    "local",
                    [new ExperimentCase<int> { Id = "case-1", Value = 1 }]),
                Task = (_, _) => throw new InvalidOperationException("execution failed"),
            },
            new ExperimentRunOptions
            {
                RunId = "run-3",
                MaxConcurrency = 1,
                RetryPolicy = new ThrowingExperimentRetryPolicy(),
            },
            _cancellationToken);

        var json = new ExperimentJsonArtifactWriter(writeIndented: false).Serialize(result);

        using var document = JsonDocument.Parse(json);
        Assert.Equal(
            "notRequested",
            document.RootElement.GetProperty("publicationStatus").GetString());
        var failure = document.RootElement
            .GetProperty("result")
            .GetProperty("items")[0]
            .GetProperty("failure");
        Assert.Equal("retryPolicyFailed", failure.GetProperty("code").GetString());
        Assert.Equal("policy", failure.GetProperty("stage").GetString());
    }

    [Fact]
    public async Task Serialize_PrerequisiteFailure_WritesItemAndPublicationFailures()
    {
        var strict = new CallbackExperimentItemScopeProvider<int, int>(
            "strict",
            isRequired: true,
            ExperimentItemScopeFailureMode.ExecutionPrerequisite,
            (_, _) => throw new InvalidOperationException("provider unavailable"));
        var later = new CallbackExperimentItemScopeProvider<int, int>(
            "later",
            isRequired: false,
            ExperimentItemScopeFailureMode.BestEffort,
            (_, _) => throw new InvalidOperationException("must not enter"));
        var result = await new ExperimentRunner().RunAsync(
            new ExperimentDefinition<int, int>
            {
                Name = "prerequisite-artifact",
                CaseSource = new LocalExperimentCaseSource<int>(
                    "local",
                    [new ExperimentCase<int> { Id = "case-1", Value = 1 }]),
                Task = (_, _) => ValueTask.FromResult(1),
                ItemScopes = [strict, later],
            },
            new ExperimentRunOptions { RunId = "run-4", MaxConcurrency = 1 },
            _cancellationToken);

        var json = new ExperimentJsonArtifactWriter(writeIndented: false).Serialize(result);

        using var document = JsonDocument.Parse(json);
        Assert.Equal(
            "failed",
            document.RootElement.GetProperty("publicationStatus").GetString());
        var item = document.RootElement.GetProperty("result").GetProperty("items")[0];
        Assert.Equal("prerequisiteFailed", item.GetProperty("status").GetString());
        Assert.Equal(
            "itemScopePrerequisiteFailed",
            item.GetProperty("failure").GetProperty("code").GetString());
        Assert.Equal(
            "publication",
            item.GetProperty("failure").GetProperty("stage").GetString());
        var publications = item.GetProperty("publications");
        Assert.Equal("failed", publications[0].GetProperty("status").GetString());
        Assert.Equal(
            "itemScopeFailed",
            publications[0].GetProperty("failure").GetProperty("code").GetString());
        Assert.Equal("notAttempted", publications[1].GetProperty("status").GetString());
    }

    [Fact]
    public async Task WriteAsync_UsesCallerProvidedJsonTypeInfoForCaseAndOutput()
    {
        var result = await new ExperimentRunner().RunAsync(
            new ExperimentDefinition<int, int>
            {
                Name = "metadata",
                CaseSource = new LocalExperimentCaseSource<int>(
                    "local",
                    [new ExperimentCase<int> { Id = "case-1", Value = 7 }]),
                Task = (_, _) => ValueTask.FromResult(9),
            },
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
            _cancellationToken);
        var serializerOptions = new JsonSerializerOptions
        {
            NumberHandling = JsonNumberHandling.WriteAsString,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };
        var caseTypeInfo = (JsonTypeInfo<int>)serializerOptions.GetTypeInfo(typeof(int));
        var outputTypeInfo = (JsonTypeInfo<int>)serializerOptions.GetTypeInfo(typeof(int));
        await using var stream = new MemoryStream();

        await new ExperimentJsonArtifactWriter(writeIndented: false).WriteAsync(
            stream,
            result,
            caseTypeInfo,
            outputTypeInfo,
            _cancellationToken);

        stream.Position = 0;
        using var document = await JsonDocument.ParseAsync(
            stream,
            cancellationToken: _cancellationToken);
        var item = document.RootElement.GetProperty("result").GetProperty("items")[0];
        Assert.Equal("7", item.GetProperty("case").GetString());
        Assert.Equal("9", item.GetProperty("output").GetString());
    }

    private async Task<ExperimentRunOutcome<int, string>> CreateSimpleOutcomeAsync() =>
        await new ExperimentRunner().RunAsync(
            new ExperimentDefinition<int, string>
            {
                Name = "overload-artifact",
                CaseSource = new LocalExperimentCaseSource<int>(
                    "local",
                    [new ExperimentCase<int> { Id = "case-1", Value = 7 }]),
                Task = (_, _) => ValueTask.FromResult("output"),
            },
            new ExperimentRunOptions
            {
                RunId = "overload-run",
                MaxConcurrency = 1,
            },
            _cancellationToken);
}
