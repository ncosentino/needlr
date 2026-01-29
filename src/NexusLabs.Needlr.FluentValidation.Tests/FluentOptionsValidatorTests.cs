using NexusLabs.Needlr.Generators;

using Xunit;

namespace NexusLabs.Needlr.FluentValidation.Tests;

public sealed class FluentOptionsValidatorTests
{
    [Fact]
    public void FluentOptionsValidator_ImplementsIOptionsValidator()
    {
        var validator = new ConcreteFluentOptionsValidator();
        IOptionsValidator<TestOptions> optionsValidator = validator;

        var errors = optionsValidator.Validate(new TestOptions { Name = "" }).ToList();

        Assert.Single(errors);
        Assert.Contains("Name", errors[0].Message);
    }

    [Fact]
    public void FluentOptionsValidator_CanBeUsedDirectly()
    {
        var validator = new ConcreteFluentOptionsValidator();

        var result = validator.Validate(new TestOptions { Name = "Valid" });

        Assert.True(result.IsValid);
    }
}
