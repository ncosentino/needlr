using FluentValidation;

using NexusLabs.Needlr.Generators;

namespace OptionsValidationExample.Options;

/// <summary>
/// API client options with URL and timeout validation.
/// </summary>
[Options("ApiClient", ValidateOnStart = true, Validator = typeof(ApiClientOptionsValidator))]
public sealed class ApiClientOptions
{
    public string BaseUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxRetries { get; set; } = 3;
    public string[] AllowedEndpoints { get; set; } = [];
}

public sealed class ApiClientOptionsValidator : AbstractValidator<ApiClientOptions>
{
    public ApiClientOptionsValidator()
    {
        RuleFor(x => x.BaseUrl)
            .NotEmpty()
            .WithMessage("Base URL is required")
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) && 
                        (uri.Scheme == "http" || uri.Scheme == "https"))
            .When(x => !string.IsNullOrEmpty(x.BaseUrl))
            .WithMessage("Base URL must be a valid HTTP/HTTPS URL");

        RuleFor(x => x.ApiKey)
            .NotEmpty()
            .WithMessage("API key is required")
            .MinimumLength(16)
            .WithMessage("API key must be at least 16 characters");

        RuleFor(x => x.TimeoutSeconds)
            .InclusiveBetween(1, 300)
            .WithMessage("Timeout must be between 1 and 300 seconds");

        RuleFor(x => x.MaxRetries)
            .InclusiveBetween(0, 10)
            .WithMessage("Max retries must be between 0 and 10");
    }
}
