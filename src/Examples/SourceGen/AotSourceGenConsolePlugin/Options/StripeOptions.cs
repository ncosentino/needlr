// Example of options with startup validation
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

using NexusLabs.Needlr.Generators;

namespace AotSourceGenConsolePlugin.Options;

/// <summary>
/// Options with DataAnnotations validation, validated at startup.
/// </summary>
[Options(ValidateOnStart = true)]
public class StripeOptions
{
    /// <summary>
    /// The Stripe API key. Must be provided.
    /// </summary>
    [Required(ErrorMessage = "Stripe API key is required")]
    public string ApiKey { get; set; } = "";

    /// <summary>
    /// The webhook secret for verifying webhook signatures.
    /// </summary>
    public string? WebhookSecret { get; set; }

    /// <summary>
    /// Custom validator method called at startup.
    /// </summary>
    [OptionsValidator]
    public IEnumerable<string> Validate()
    {
        if (!string.IsNullOrEmpty(ApiKey) && !ApiKey.StartsWith("sk_"))
        {
            yield return "ApiKey must start with 'sk_'";
        }

        if (!string.IsNullOrEmpty(WebhookSecret) && !WebhookSecret.StartsWith("whsec_"))
        {
            yield return "WebhookSecret must start with 'whsec_'";
        }
    }
}
