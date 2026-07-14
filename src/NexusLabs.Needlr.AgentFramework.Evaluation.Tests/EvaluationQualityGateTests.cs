using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests;

public sealed class EvaluationQualityGateTests
{
    [Fact]
    public void Assert_AllThresholdsMet_DoesNotThrow()
    {
        var result = new EvaluationResult(
            new NumericMetric("Total Tokens", value: 500),
            new BooleanMetric("All Tool Calls Succeeded", value: true));

        var gate = new EvaluationQualityGate()
            .RequireNumericMax("Total Tokens", max: 1000)
            .RequireBoolean("All Tool Calls Succeeded", expected: true);

        gate.Assert(result);

        var evaluation = gate.Evaluate(result);
        Assert.Equal(EvaluationDecision.Passed, evaluation.Decision);
        Assert.Equal(2, evaluation.Outcomes.Count);
        Assert.All(
            evaluation.Outcomes,
            outcome => Assert.Equal(EvaluationThresholdStatus.Passed, outcome.Status));
    }

    [Fact]
    public void Assert_NumericExceedsMax_Throws()
    {
        var result = new EvaluationResult(
            new NumericMetric("Total Tokens", value: 1500));

        var gate = new EvaluationQualityGate()
            .RequireNumericMax("Total Tokens", max: 1000);

        var ex = Assert.Throws<QualityGateFailedException>(() => gate.Assert(result));
        Assert.Single(ex.Violations);
        Assert.Contains("Total Tokens", ex.Violations[0]);
        Assert.Contains("exceeded max", ex.Violations[0]);
    }

    [Fact]
    public void Assert_NumericAtExactMax_Passes()
    {
        var result = new EvaluationResult(
            new NumericMetric("Total Tokens", value: 1000));

        var gate = new EvaluationQualityGate()
            .RequireNumericMax("Total Tokens", max: 1000);

        gate.Assert(result);
    }

    [Fact]
    public void Assert_NumericBelowMin_Throws()
    {
        var result = new EvaluationResult(
            new NumericMetric("Iteration Efficiency Ratio", value: 0.3));

        var gate = new EvaluationQualityGate()
            .RequireNumericMin("Iteration Efficiency Ratio", min: 0.5);

        var ex = Assert.Throws<QualityGateFailedException>(() => gate.Assert(result));
        Assert.Single(ex.Violations);
        Assert.Contains("below min", ex.Violations[0]);
    }

    [Fact]
    public void Assert_NumericAtExactMin_Passes()
    {
        var result = new EvaluationResult(
            new NumericMetric("Iteration Efficiency Ratio", value: 0.5));

        var gate = new EvaluationQualityGate()
            .RequireNumericMin("Iteration Efficiency Ratio", min: 0.5);

        gate.Assert(result);
    }

    [Fact]
    public void Assert_BooleanMismatch_Throws()
    {
        var result = new EvaluationResult(
            new BooleanMetric("All Tool Calls Succeeded", value: false));

        var gate = new EvaluationQualityGate()
            .RequireBoolean("All Tool Calls Succeeded", expected: true);

        var ex = Assert.Throws<QualityGateFailedException>(() => gate.Assert(result));
        Assert.Single(ex.Violations);
        Assert.Contains("expected True", ex.Violations[0]);
    }

    [Fact]
    public void Evaluate_MissingMetric_IsInconclusiveAndAssertThrows()
    {
        var result = new EvaluationResult(
            new NumericMetric("Total Tokens", value: 500));

        var gate = new EvaluationQualityGate()
            .RequireNumericMax("Total Tokens", max: 1000)
            .RequireBoolean("Max Iterations Hit", expected: false);

        var evaluation = gate.Evaluate(result);

        Assert.Equal(EvaluationDecision.Inconclusive, evaluation.Decision);
        Assert.Equal(
            EvaluationThresholdStatus.Missing,
            evaluation.Outcomes[1].Status);
        var exception = Assert.Throws<QualityGateFailedException>(() => gate.Assert(result));
        Assert.Contains("Max Iterations Hit", Assert.Single(exception.Violations));
    }

    [Fact]
    public void Assert_MultipleViolations_AllReported()
    {
        var result = new EvaluationResult(
            new NumericMetric("Total Tokens", value: 2000),
            new BooleanMetric("All Tool Calls Succeeded", value: false),
            new NumericMetric("Iteration Efficiency Ratio", value: 0.2));

        var gate = new EvaluationQualityGate()
            .RequireNumericMax("Total Tokens", max: 1000)
            .RequireBoolean("All Tool Calls Succeeded", expected: true)
            .RequireNumericMin("Iteration Efficiency Ratio", min: 0.5);

        var ex = Assert.Throws<QualityGateFailedException>(() => gate.Assert(result));
        Assert.Equal(3, ex.Violations.Count);
    }

    [Fact]
    public void Assert_MultipleResults_FindsMetricsAcrossThem()
    {
        var trajectoryResult = new EvaluationResult(
            new BooleanMetric("All Tool Calls Succeeded", value: true));
        var efficiencyResult = new EvaluationResult(
            new NumericMetric("Total Tokens", value: 500));

        var gate = new EvaluationQualityGate()
            .RequireBoolean("All Tool Calls Succeeded", expected: true)
            .RequireNumericMax("Total Tokens", max: 1000);

        gate.Assert(trajectoryResult, efficiencyResult);
    }

    [Fact]
    public void Assert_MultipleResults_FirstMatchWins()
    {
        var result1 = new EvaluationResult(
            new NumericMetric("Total Tokens", value: 500));
        var result2 = new EvaluationResult(
            new NumericMetric("Total Tokens", value: 9999));

        var gate = new EvaluationQualityGate()
            .RequireNumericMax("Total Tokens", max: 1000);

        gate.Assert(result1, result2);
    }

    [Fact]
    public void Evaluate_EmptyResultsWithRequiredThreshold_IsInconclusive()
    {
        var gate = new EvaluationQualityGate()
            .RequireNumericMax("Total Tokens", max: 1000);

        var evaluation = gate.Evaluate(new EvaluationResult());

        Assert.Equal(EvaluationDecision.Inconclusive, evaluation.Decision);
        Assert.Throws<QualityGateFailedException>(() => gate.Assert(new EvaluationResult()));
    }

    [Fact]
    public void Assert_NoThresholds_DoesNotThrow()
    {
        var result = new EvaluationResult(
            new NumericMetric("Total Tokens", value: 999999));

        var gate = new EvaluationQualityGate();

        gate.Assert(result);
    }

    [Fact]
    public void Evaluate_NullValue_IsInvalidAndCanBeTreatedAsFailure()
    {
        var result = new EvaluationResult(
            new NumericMetric("Total Tokens", value: null));

        var gate = new EvaluationQualityGate()
            .RequireNumericMax("Total Tokens", max: 1000);

        var inconclusive = gate.Evaluate(result);
        var pessimistic = gate.Evaluate(EvaluationMissingMetricBehavior.Fail, result);

        Assert.Equal(EvaluationDecision.Inconclusive, inconclusive.Decision);
        Assert.Equal(EvaluationThresholdStatus.Invalid, Assert.Single(inconclusive.Outcomes).Status);
        Assert.Equal(EvaluationDecision.Failed, pessimistic.Decision);
        Assert.Throws<QualityGateFailedException>(() => gate.Assert(result));
    }

    [Fact]
    public void Evaluate_MissingOptionalMetric_DoesNotBlockPassingDecision()
    {
        var result = new EvaluationResult(
            new NumericMetric("Total Tokens", value: 500));
        var gate = new EvaluationQualityGate()
            .RequireNumericMax("Total Tokens", max: 1000)
            .OptionalBoolean("Iteration Coherent", expected: true);

        var evaluation = gate.Evaluate(result);

        Assert.Equal(EvaluationDecision.Passed, evaluation.Decision);
        Assert.Equal(
            [EvaluationThresholdStatus.Passed, EvaluationThresholdStatus.Missing],
            evaluation.Outcomes.Select(outcome => outcome.Status));
        gate.Assert(result);
    }

    [Fact]
    public void Evaluate_InvalidOptionalMetric_IsInconclusive()
    {
        var result = new EvaluationResult(
            new StringMetric("Iteration Coherent", value: "not-a-boolean"));
        var gate = new EvaluationQualityGate()
            .OptionalBoolean("Iteration Coherent", expected: true);

        var evaluation = gate.Evaluate(result);

        Assert.Equal(EvaluationDecision.Inconclusive, evaluation.Decision);
        Assert.Equal(EvaluationThresholdStatus.Invalid, Assert.Single(evaluation.Outcomes).Status);
        Assert.Throws<QualityGateFailedException>(() => gate.Assert(result));
    }

    [Fact]
    public void QualityGateFailedException_MessageListsViolations()
    {
        var violations = new List<string> { "A: exceeded", "B: below" };
        var ex = new QualityGateFailedException(violations);

        Assert.Contains("2 violation(s)", ex.Message);
        Assert.Contains("A: exceeded", ex.Message);
        Assert.Contains("B: below", ex.Message);
    }
}
