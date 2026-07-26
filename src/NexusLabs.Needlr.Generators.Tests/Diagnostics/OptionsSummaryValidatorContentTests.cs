using System.Linq;

using Xunit;

using static NexusLabs.Needlr.Generators.Tests.Diagnostics.DiagnosticModelFactory;

namespace NexusLabs.Needlr.Generators.Tests.Diagnostics;

public sealed class OptionsSummaryValidatorContentTests
{
    [Fact]
    public void ExternalValidator_IsDescribedInTableAndSectionDetails()
    {
        var discovery = new DiagnosticDiscoveryBuilder()
            .WithOptions(OptionsWithExternalValidator(
                "global::TestApp.DatabaseOptions",
                "Database",
                "global::TestApp.DatabaseOptionsValidator",
                validateOnStart: true))
            .Build();

        var document = MarkdownDocument.Parse(
            DiagnosticArtifactRenderer.OptionsSummary(discovery, DiagnosticArtifactRenderer.NoFilter()));

        Assert.Equal(
            "`DatabaseOptionsValidator`",
            document.Section("Options Classes").Tables.Single().Cell(0, "Validator"));
        Assert.Contains(
            "- External Validator: `DatabaseOptionsValidator`",
            document.Section("`Database`").Lines);
        Assert.Contains("- Validate On Start: Yes", document.Section("`Database`").Lines);
    }

    [Theory]
    [InlineData(true, "`Validate()` (static)", "- Validation Method: `Validate()` (static)")]
    [InlineData(false, "`Validate()` (self)", "- Validation Method: `Validate()` (instance)")]
    public void ValidatorMethod_DescribesStaticAndInstanceForms(bool isStatic, string tableValue, string detailLine)
    {
        var discovery = new DiagnosticDiscoveryBuilder()
            .WithOptions(OptionsWithValidatorMethod("global::TestApp.CacheOptions", "Cache", "Validate", isStatic))
            .Build();

        var document = MarkdownDocument.Parse(
            DiagnosticArtifactRenderer.OptionsSummary(discovery, DiagnosticArtifactRenderer.NoFilter()));

        Assert.Equal(tableValue, document.Section("Options Classes").Tables.Single().Cell(0, "Validator"));
        Assert.Contains(detailLine, document.Section("`Cache`").Lines);
    }

    [Fact]
    public void ValidatorMethodOverrideWithoutMatch_IsReportedAsNotFound()
    {
        var discovery = new DiagnosticDiscoveryBuilder()
            .WithOptions(OptionsWithMissingValidatorMethod("global::TestApp.CacheOptions", "Cache", "CheckIt"))
            .Build();

        var document = MarkdownDocument.Parse(
            DiagnosticArtifactRenderer.OptionsSummary(discovery, DiagnosticArtifactRenderer.NoFilter()));

        Assert.Equal(
            "`CheckIt()` (not found)",
            document.Section("Options Classes").Tables.Single().Cell(0, "Validator"));
        Assert.Contains(
            "- Validation Method: `CheckIt()` (specified but not found)",
            document.Section("`Cache`").Lines);
    }

    [Fact]
    public void OptionsWithoutValidator_RenderDashAndNoIssuesSection()
    {
        var discovery = new DiagnosticDiscoveryBuilder()
            .WithOptions(OptionsType("global::TestApp.CacheOptions", "Cache"))
            .Build();

        var document = MarkdownDocument.Parse(
            DiagnosticArtifactRenderer.OptionsSummary(discovery, DiagnosticArtifactRenderer.NoFilter()));

        var table = document.Section("Options Classes").Tables.Single();

        Assert.Equal("-", table.Cell(0, "Validator"));
        Assert.Equal("-", table.Cell(0, "Name"));
        Assert.Equal("❌", table.Cell(0, "ValidateOnStart"));
        Assert.False(
            document.HasSection("⚠️ Potential Issues"),
            "Expected no potential issues section when no validator is configured");
    }

    [Fact]
    public void ValidatorWithoutValidateOnStart_IsReportedAsPotentialIssue()
    {
        var discovery = new DiagnosticDiscoveryBuilder()
            .WithOptions(OptionsWithExternalValidator(
                "global::TestApp.DatabaseOptions",
                "Database",
                "global::TestApp.DatabaseOptionsValidator",
                validateOnStart: false))
            .Build();

        var document = MarkdownDocument.Parse(
            DiagnosticArtifactRenderer.OptionsSummary(discovery, DiagnosticArtifactRenderer.NoFilter()));

        Assert.Contains(
            "- `DatabaseOptions`: Has validator but won't run at startup",
            document.Section("⚠️ Potential Issues").Lines);
        Assert.Contains("- Validate On Start: No", document.Section("`Database`").Lines);
    }

    [Fact]
    public void Overview_CountsNamedValidatedAndExternallyValidatedOptions()
    {
        var discovery = new DiagnosticDiscoveryBuilder()
            .WithOptions(
                NamedOptions("global::TestApp.ConnectionOptions", "Connections", "Primary"),
                OptionsWithExternalValidator(
                    "global::TestApp.DatabaseOptions",
                    "Database",
                    "global::TestApp.DatabaseOptionsValidator",
                    validateOnStart: true),
                OptionsType("global::TestApp.CacheOptions", "Cache"))
            .Build();

        var document = MarkdownDocument.Parse(
            DiagnosticArtifactRenderer.OptionsSummary(discovery, DiagnosticArtifactRenderer.NoFilter()));

        var overview = document.Section("Overview").Tables.Single();

        Assert.Equal(new[] { "3", "1", "1", "1" }, overview.Column("Count"));
        Assert.Equal(
            new[] { "`Cache`", "`Connections`", "`Database`" },
            document.Section("Options Classes").Tables.Single().Column("Section"));
        Assert.Equal(
            new[] { "`Cache`", "`Connections`", "`Database`" },
            document.Sections.Where(s => s.Level == 3).Select(s => s.Title).ToArray());
    }

    [Fact]
    public void NamedOptions_RenderNameInTableAndSectionDetails()
    {
        var discovery = new DiagnosticDiscoveryBuilder()
            .WithOptions(NamedOptions("global::TestApp.ConnectionOptions", "Connections", "Primary"))
            .Build();

        var document = MarkdownDocument.Parse(
            DiagnosticArtifactRenderer.OptionsSummary(discovery, DiagnosticArtifactRenderer.NoFilter()));

        Assert.Equal("Primary", document.Section("Options Classes").Tables.Single().Cell(0, "Name"));
        Assert.Contains("- Named Options: `Primary`", document.Section("`Connections`").Lines);
        Assert.Contains("- Configuration Path: `Connections`", document.Section("`Connections`").Lines);
    }
}
