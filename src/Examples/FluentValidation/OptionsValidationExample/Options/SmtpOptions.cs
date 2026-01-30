using FluentValidation;

using NexusLabs.Needlr.Generators;

namespace OptionsValidationExample.Options;

/// <summary>
/// SMTP email options with comprehensive validation rules.
/// </summary>
[Options("Smtp", ValidateOnStart = true, Validator = typeof(SmtpOptionsValidator))]
public sealed class SmtpOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public bool UseSsl { get; set; } = true;
    public string FromAddress { get; set; } = "";
    public string FromName { get; set; } = "";
}

public sealed class SmtpOptionsValidator : AbstractValidator<SmtpOptions>
{
    public SmtpOptionsValidator()
    {
        RuleFor(x => x.Host)
            .NotEmpty()
            .WithMessage("SMTP host is required");

        RuleFor(x => x.Port)
            .InclusiveBetween(1, 65535)
            .WithMessage("Port must be a valid TCP port (1-65535)");

        RuleFor(x => x.Username)
            .NotEmpty()
            .When(x => !string.IsNullOrEmpty(x.Password))
            .WithMessage("Username is required when password is provided");

        RuleFor(x => x.FromAddress)
            .NotEmpty()
            .WithMessage("From address is required")
            .EmailAddress()
            .When(x => !string.IsNullOrEmpty(x.FromAddress))
            .WithMessage("From address must be a valid email");
    }
}
