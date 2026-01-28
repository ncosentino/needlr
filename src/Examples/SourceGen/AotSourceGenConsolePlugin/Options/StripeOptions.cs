// Example of options with startup validation
using System.Collections.Generic;

using NexusLabs.Needlr.Generators;

namespace AotSourceGenConsolePlugin.Options;

/// <summary>
/// Options with custom validation, validated at startup.
/// </summary>
[Options(ValidateOnStart = true)]
public class StripeOptions
{
    /// <summary>
    /// The Stripe API key. Must be provided.
    /// </summary>
    public string ApiKey { get; set; } = "";

    /// <summary>
    /// The webhook secret for verifying webhook signatures.
    /// </summary>
    public string? WebhookSecret { get; set; }

    /// <summary>
    /// Custom validator method called at startup.
    /// Convention: just name it "Validate" - no attribute needed.
    /// </summary>
    public IEnumerable<ValidationError> Validate()
    {
        if (string.IsNullOrEmpty(ApiKey))
        {
            yield return "Stripe API key is required";
        }
        else if (!ApiKey.StartsWith("sk_"))
        {
            yield return "ApiKey must start with 'sk_'";
        }

        if (!string.IsNullOrEmpty(WebhookSecret) && !WebhookSecret.StartsWith("whsec_"))
        {
            yield return "WebhookSecret must start with 'whsec_'";
        }
    }
}
