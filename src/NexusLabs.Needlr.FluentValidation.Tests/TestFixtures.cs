using FluentValidation;

namespace NexusLabs.Needlr.FluentValidation.Tests;

public class TestOptions
{
    public string Name { get; set; } = "";
    public int Count { get; set; }
}

public class TestOptionsValidator : AbstractValidator<TestOptions>
{
    public TestOptionsValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required");
    }
}

public class WarningOnlyValidator : AbstractValidator<TestOptions>
{
    public WarningOnlyValidator()
    {
        RuleFor(x => x.Name)
            .Must(n => !n.Contains("warning"))
            .WithMessage("Warning triggered")
            .WithSeverity(Severity.Warning);
    }
}

public class ConcreteFluentOptionsValidator : FluentOptionsValidator<TestOptions>
{
    public ConcreteFluentOptionsValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required");
    }
}
