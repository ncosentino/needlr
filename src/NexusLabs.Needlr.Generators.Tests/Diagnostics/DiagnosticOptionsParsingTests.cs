using Xunit;

namespace NexusLabs.Needlr.Generators.Tests.Diagnostics;

public sealed class DiagnosticOptionsParsingTests
{
    [Fact]
    public void DiagnosticOptions_Parse_EnabledTrue_ReturnsEnabled()
    {
        var options = DiagnosticOptions.Parse("true", null, null);

        Assert.True(options.Enabled);
    }

    [Fact]
    public void DiagnosticOptions_Parse_Enabled1_ReturnsEnabled()
    {
        var options = DiagnosticOptions.Parse("1", null, null);

        Assert.True(options.Enabled);
    }

    [Fact]
    public void DiagnosticOptions_Parse_EnabledTRUE_CaseInsensitive()
    {
        var options = DiagnosticOptions.Parse("TRUE", null, null);

        Assert.True(options.Enabled);
    }

    [Fact]
    public void DiagnosticOptions_Parse_EnabledFalse_ReturnsDisabled()
    {
        var options = DiagnosticOptions.Parse("false", null, null);

        Assert.False(options.Enabled);
    }

    [Fact]
    public void DiagnosticOptions_Parse_EnabledNull_ReturnsDisabled()
    {
        var options = DiagnosticOptions.Parse(null, null, null);

        Assert.False(options.Enabled);
    }

    [Fact]
    public void DiagnosticOptions_Parse_EnabledEmpty_ReturnsDisabled()
    {
        var options = DiagnosticOptions.Parse("", null, null);

        Assert.False(options.Enabled);
    }

    [Fact]
    public void DiagnosticOptions_Parse_EnabledWhitespace_ReturnsDisabled()
    {
        var options = DiagnosticOptions.Parse("   ", null, null);

        Assert.False(options.Enabled);
    }

    [Fact]
    public void DiagnosticOptions_Parse_OutputPath_IsTrimmed()
    {
        var options = DiagnosticOptions.Parse("true", "  MyPath  ", null);

        Assert.Equal("MyPath", options.OutputPath);
    }

    [Fact]
    public void DiagnosticOptions_Parse_OutputPathNull_ReturnsEmpty()
    {
        var options = DiagnosticOptions.Parse("true", null, null);

        Assert.Equal(string.Empty, options.OutputPath);
    }

    [Fact]
    public void DiagnosticOptions_Parse_Filter_SingleType()
    {
        var options = DiagnosticOptions.Parse("true", null, "MyApp.Services.OrderService");

        Assert.Single(options.TypeFilter);
        Assert.Contains("MyApp.Services.OrderService", options.TypeFilter);
    }

    [Fact]
    public void DiagnosticOptions_Parse_Filter_MultipleTypesComma()
    {
        var options = DiagnosticOptions.Parse("true", null, "MyApp.OrderService,MyApp.PaymentService");

        Assert.Equal(2, options.TypeFilter.Count);
        Assert.Contains("MyApp.OrderService", options.TypeFilter);
        Assert.Contains("MyApp.PaymentService", options.TypeFilter);
    }

    [Fact]
    public void DiagnosticOptions_Parse_Filter_MultipleTypesSemicolon()
    {
        var options = DiagnosticOptions.Parse("true", null, "MyApp.OrderService;MyApp.PaymentService");

        Assert.Equal(2, options.TypeFilter.Count);
        Assert.Contains("MyApp.OrderService", options.TypeFilter);
        Assert.Contains("MyApp.PaymentService", options.TypeFilter);
    }

    [Fact]
    public void DiagnosticOptions_Parse_Filter_TrimsParts()
    {
        var options = DiagnosticOptions.Parse("true", null, "  MyApp.OrderService  ,  MyApp.PaymentService  ");

        Assert.Equal(2, options.TypeFilter.Count);
        Assert.Contains("MyApp.OrderService", options.TypeFilter);
        Assert.Contains("MyApp.PaymentService", options.TypeFilter);
    }

    [Fact]
    public void DiagnosticOptions_Parse_Filter_IgnoresEmptyParts()
    {
        var options = DiagnosticOptions.Parse("true", null, "MyApp.OrderService,,MyApp.PaymentService,");

        Assert.Equal(2, options.TypeFilter.Count);
    }

    [Fact]
    public void DiagnosticOptions_Parse_FilterNull_ReturnsEmptySet()
    {
        var options = DiagnosticOptions.Parse("true", null, null);

        Assert.Empty(options.TypeFilter);
    }

    [Fact]
    public void DiagnosticOptions_Parse_FilterEmpty_ReturnsEmptySet()
    {
        var options = DiagnosticOptions.Parse("true", null, "");

        Assert.Empty(options.TypeFilter);
    }
}
