using Microsoft.Extensions.AI.Evaluation;

using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests.Experiments;

public sealed class ExperimentMetricSnapshotTests
{
    [Fact]
    public void Factory_NormalizesEveryMetricKindInOrdinalOrder()
    {
        var evaluation = new EvaluationResult(
            new NumericMetric("z_finite", 0.5, "finite"),
            new BooleanMetric("m_boolean", true),
            new StringMetric("s_string", "value"),
            new NumericMetric("a_nan", double.NaN, "not finite"),
            new EvaluationMetric("b_none", "no typed value"));

        var snapshots = ExperimentMetricSnapshotFactory.Create(evaluation);

        Assert.Equal(
            ["a_nan", "b_none", "m_boolean", "s_string", "z_finite"],
            snapshots.Select(snapshot => snapshot.Name));

        var nan = snapshots.Single(snapshot => snapshot.Name == "a_nan");
        Assert.Equal(ExperimentMetricKind.Numeric, nan.Kind);
        Assert.Null(nan.NumericValue);
        Assert.Equal(ExperimentMetricNonFiniteValue.NaN, nan.NonFiniteNumericValue);

        var finite = snapshots.Single(snapshot => snapshot.Name == "z_finite");
        Assert.Equal(0.5, finite.NumericValue);
        Assert.Null(finite.NonFiniteNumericValue);

        var boolean = snapshots.Single(snapshot => snapshot.Name == "m_boolean");
        Assert.Equal(ExperimentMetricKind.Boolean, boolean.Kind);
        Assert.True(
            boolean.BooleanValue,
            "Expected the normalized boolean metric to preserve its true value.");

        var text = snapshots.Single(snapshot => snapshot.Name == "s_string");
        Assert.Equal(ExperimentMetricKind.String, text.Kind);
        Assert.Equal("value", text.StringValue);

        var none = snapshots.Single(snapshot => snapshot.Name == "b_none");
        Assert.Equal(ExperimentMetricKind.None, none.Kind);
    }

    [Fact]
    public void Constructor_SnapshotsCollections_IndependentlyOfSource()
    {
        var diagnostics = new List<ExperimentMetricDiagnosticSnapshot>
        {
            new()
            {
                Severity = ExperimentMetricDiagnosticSeverity.Warning,
                Message = "first",
            },
        };
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["key"] = "value",
        };

        var snapshot = new ExperimentMetricSnapshot(
            "name",
            ExperimentMetricKind.Numeric,
            numericValue: 1.0,
            nonFiniteNumericValue: null,
            booleanValue: null,
            stringValue: null,
            reason: null,
            interpretation: null,
            contextCount: 0,
            diagnostics,
            metadata);

        diagnostics.Add(new ExperimentMetricDiagnosticSnapshot
        {
            Severity = ExperimentMetricDiagnosticSeverity.Error,
            Message = "second",
        });
        metadata["added"] = "later";

        Assert.Single(snapshot.Diagnostics);
        Assert.Single(snapshot.Metadata);
        Assert.Equal("value", snapshot.Metadata["key"]);
    }

    [Fact]
    public void Constructor_OrdersMetadataByOrdinalKey()
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["b"] = "2",
            ["a"] = "1",
            ["c"] = "3",
        };

        var snapshot = new ExperimentMetricSnapshot(
            "name",
            ExperimentMetricKind.None,
            numericValue: null,
            nonFiniteNumericValue: null,
            booleanValue: null,
            stringValue: null,
            reason: null,
            interpretation: null,
            contextCount: 0,
            [],
            metadata);

        Assert.Equal(["a", "b", "c"], snapshot.Metadata.Keys);
    }

    [Theory]
    [MemberData(nameof(InvalidValueUnions))]
    public void Constructor_InvalidValueUnion_Throws(
        ExperimentMetricKind kind,
        double? numericValue,
        ExperimentMetricNonFiniteValue? nonFiniteNumericValue,
        bool? booleanValue,
        string? stringValue)
    {
        Assert.Throws<ArgumentException>(() => new ExperimentMetricSnapshot(
            "name",
            kind,
            numericValue,
            nonFiniteNumericValue,
            booleanValue,
            stringValue,
            reason: null,
            interpretation: null,
            contextCount: 0,
            [],
            new Dictionary<string, string>()));
    }

    public static TheoryData<
        ExperimentMetricKind,
        double?,
        ExperimentMetricNonFiniteValue?,
        bool?,
        string?> InvalidValueUnions => new()
    {
        { ExperimentMetricKind.Numeric, 1.0, null, true, null },
        { ExperimentMetricKind.Numeric, 1.0, null, null, "x" },
        { ExperimentMetricKind.Numeric, 1.0, ExperimentMetricNonFiniteValue.NaN, null, null },
        { ExperimentMetricKind.Boolean, 1.0, null, true, null },
        { ExperimentMetricKind.Boolean, null, ExperimentMetricNonFiniteValue.NaN, true, null },
        { ExperimentMetricKind.String, null, null, true, "x" },
        { ExperimentMetricKind.None, 1.0, null, null, null },
        { ExperimentMetricKind.Unknown, null, null, null, "x" },
    };

    [Fact]
    public void Constructor_BlankName_Throws()
    {
        Assert.Throws<ArgumentException>(() => Create(name: "  "));
    }

    [Fact]
    public void Constructor_UndefinedKind_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExperimentMetricSnapshot(
            "name",
            (ExperimentMetricKind)99,
            numericValue: null,
            nonFiniteNumericValue: null,
            booleanValue: null,
            stringValue: null,
            reason: null,
            interpretation: null,
            contextCount: 0,
            [],
            new Dictionary<string, string>()));
    }

    [Fact]
    public void Constructor_NegativeContextCount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(contextCount: -1));
    }

    [Fact]
    public void Constructor_NullDiagnosticsOrMetadata_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ExperimentMetricSnapshot(
            "name",
            ExperimentMetricKind.None,
            null,
            null,
            null,
            null,
            null,
            null,
            0,
            null!,
            new Dictionary<string, string>()));
        Assert.Throws<ArgumentNullException>(() => new ExperimentMetricSnapshot(
            "name",
            ExperimentMetricKind.None,
            null,
            null,
            null,
            null,
            null,
            null,
            0,
            [],
            null!));
    }

    private static ExperimentMetricSnapshot Create(
        string name = "name",
        int contextCount = 0) =>
        new(
            name,
            ExperimentMetricKind.None,
            numericValue: null,
            nonFiniteNumericValue: null,
            booleanValue: null,
            stringValue: null,
            reason: null,
            interpretation: null,
            contextCount,
            [],
            new Dictionary<string, string>());
}
