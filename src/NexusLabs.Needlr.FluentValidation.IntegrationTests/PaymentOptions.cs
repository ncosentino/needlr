using FluentValidation;

using NexusLabs.Needlr.Generators;

namespace NexusLabs.Needlr.FluentValidation.IntegrationTests;

[Options(ValidateOnStart = true, Validator = typeof(PaymentOptionsValidator))]
public sealed class PaymentOptions
{
    public string MerchantId { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public int MaxRetries { get; set; } = 3;
}

public sealed class PaymentOptionsValidator : AbstractValidator<PaymentOptions>
{
    public PaymentOptionsValidator()
    {
        RuleFor(x => x.MerchantId)
            .NotEmpty()
            .WithMessage("Merchant ID is required");

        RuleFor(x => x.ApiKey)
            .NotEmpty()
            .WithMessage("API key is required")
            .Must(x => x.StartsWith("pk_") || x.StartsWith("sk_"))
            .When(x => !string.IsNullOrEmpty(x.ApiKey))
            .WithMessage("API key must start with 'pk_' or 'sk_'");

        RuleFor(x => x.MaxRetries)
            .InclusiveBetween(1, 10)
            .WithMessage("Max retries must be between 1 and 10");
    }
}
