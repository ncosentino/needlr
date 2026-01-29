using FluentValidation;

using Xunit;

namespace NexusLabs.Needlr.FluentValidation.Tests;

public sealed class FluentValidationOptionsAdapterTests
{
    [Fact]
    public void Validate_WithValidOptions_ReturnsSuccess()
    {
        var validator = new TestOptionsValidator();
        var adapter = new FluentValidationOptionsAdapter<TestOptions>(validator);

        var result = adapter.Validate(null, new TestOptions { Name = "Valid" });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WithInvalidOptions_ReturnsFailure()
    {
        var validator = new TestOptionsValidator();
        var adapter = new FluentValidationOptionsAdapter<TestOptions>(validator);

        var result = adapter.Validate(null, new TestOptions { Name = "" });

        Assert.True(result.Failed);
        Assert.Contains("Name", result.FailureMessage);
    }

    [Fact]
    public void Validate_WithNamedOptions_SkipsWrongName()
    {
        var validator = new TestOptionsValidator();
        var adapter = new FluentValidationOptionsAdapter<TestOptions>(validator, "expected");

        var result = adapter.Validate("other", new TestOptions { Name = "" });

        Assert.True(result.Skipped);
    }

    [Fact]
    public void Validate_WithNamedOptions_ValidatesMatchingName()
    {
        var validator = new TestOptionsValidator();
        var adapter = new FluentValidationOptionsAdapter<TestOptions>(validator, "expected");

        var result = adapter.Validate("expected", new TestOptions { Name = "" });

        Assert.True(result.Failed);
    }

    [Fact]
    public void Validate_WithNullOptions_ReturnsFailure()
    {
        var validator = new TestOptionsValidator();
        var adapter = new FluentValidationOptionsAdapter<TestOptions>(validator);

        var result = adapter.Validate(null, null!);

        Assert.True(result.Failed);
        Assert.Contains("null", result.FailureMessage);
    }

    [Fact]
    public void Validate_WithWarnings_DoesNotFail()
    {
        var validator = new WarningOnlyValidator();
        var adapter = new FluentValidationOptionsAdapter<TestOptions>(validator);

        var result = adapter.Validate(null, new TestOptions { Name = "triggers-warning" });

        Assert.True(result.Succeeded);
    }
}
