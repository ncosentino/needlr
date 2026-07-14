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
                "runId",
                "experimentName",
                "source",
                "startedAt",
                "durationMilliseconds",
                "maxConcurrency",
                "workerCount",
                "items",
            ],
            root.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("run-1", root.GetProperty("runId").GetString());

        var items = root.GetProperty("items");
        Assert.Equal(2, items.GetArrayLength());
        var success = items[0];
        Assert.Equal("succeeded", success.GetProperty("status").GetString());
        Assert.Equal("7", success.GetProperty("case").GetString());
        Assert.Equal("output", success.GetProperty("output").GetString());
        Assert.False(success.TryGetProperty("evaluation", out _));
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

        var failure = items[1];
        Assert.Equal("executionFailed", failure.GetProperty("status").GetString());
        var failurePayload = failure.GetProperty("failure");
        Assert.Equal("executionFailed", failurePayload.GetProperty("code").GetString());
        Assert.Equal("execution", failurePayload.GetProperty("stage").GetString());
        Assert.Equal(typeof(InvalidOperationException).FullName, failurePayload.GetProperty("exceptionType").GetString());
        Assert.Equal("boom", failurePayload.GetProperty("message").GetString());
        Assert.False(
            failurePayload.GetProperty("isRetryable").GetBoolean(),
            "Phase 1 failures must not claim retry-policy eligibility.");
        Assert.DoesNotContain("stack", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("$type", json, StringComparison.Ordinal);
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
        var item = document.RootElement.GetProperty("items")[0];
        Assert.Equal("7", item.GetProperty("case").GetString());
        Assert.Equal("9", item.GetProperty("output").GetString());
    }
}
