using FluentValidation;

using NexusLabs.Needlr.Generators;

using Xunit;

namespace NexusLabs.Needlr.FluentValidation.Tests;

public sealed class ValidationFailureConverterTests
{
    [Fact]
    public void ToValidationError_MapsAllProperties()
    {
        var failure = new global::FluentValidation.Results.ValidationFailure("PropertyName", "Error message")
        {
            ErrorCode = "ERR001",
            Severity = Severity.Warning
        };

        var error = failure.ToValidationError();

        Assert.Equal("Error message", error.Message);
        Assert.Equal("PropertyName", error.PropertyName);
        Assert.Equal("ERR001", error.ErrorCode);
        Assert.Equal(ValidationSeverity.Warning, error.Severity);
    }

    [Fact]
    public void ToValidationErrors_ConvertsCollection()
    {
        var failures = new[]
        {
            new global::FluentValidation.Results.ValidationFailure("Prop1", "Error 1"),
            new global::FluentValidation.Results.ValidationFailure("Prop2", "Error 2")
        };

        var errors = failures.ToValidationErrors().ToList();

        Assert.Equal(2, errors.Count);
        Assert.Equal("Prop1", errors[0].PropertyName);
        Assert.Equal("Prop2", errors[1].PropertyName);
    }
}
