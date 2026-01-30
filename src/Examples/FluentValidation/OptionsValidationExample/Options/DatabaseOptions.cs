using FluentValidation;

using NexusLabs.Needlr.Generators;

namespace OptionsValidationExample.Options;

/// <summary>
/// Database connection options validated with FluentValidation.
/// </summary>
[Options("Database", ValidateOnStart = true, Validator = typeof(DatabaseOptionsValidator))]
public sealed class DatabaseOptions
{
    public string ConnectionString { get; set; } = "";
    public int MaxPoolSize { get; set; } = 100;
    public int CommandTimeoutSeconds { get; set; } = 30;
    public bool EnableRetryOnFailure { get; set; } = true;
}

public sealed class DatabaseOptionsValidator : AbstractValidator<DatabaseOptions>
{
    public DatabaseOptionsValidator()
    {
        RuleFor(x => x.ConnectionString)
            .NotEmpty()
            .WithMessage("Database connection string is required");

        RuleFor(x => x.MaxPoolSize)
            .InclusiveBetween(1, 1000)
            .WithMessage("Max pool size must be between 1 and 1000");

        RuleFor(x => x.CommandTimeoutSeconds)
            .InclusiveBetween(1, 300)
            .WithMessage("Command timeout must be between 1 and 300 seconds");
    }
}
