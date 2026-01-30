using Microsoft.Extensions.Options;

using NexusLabs.Needlr;

using OptionsValidationExample.Options;

namespace OptionsValidationExample.Services;

/// <summary>
/// Example service that uses validated options.
/// </summary>
public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string body);
}

[RegisterAs<IEmailService>]
public sealed class EmailService : IEmailService
{
    private readonly SmtpOptions _smtpOptions;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<SmtpOptions> smtpOptions, ILogger<EmailService> logger)
    {
        _smtpOptions = smtpOptions.Value;
        _logger = logger;
    }

    public Task SendEmailAsync(string to, string subject, string body)
    {
        _logger.LogInformation(
            "Sending email from {From} to {To} via {Host}:{Port}",
            _smtpOptions.FromAddress,
            to,
            _smtpOptions.Host,
            _smtpOptions.Port);

        // In a real app, you'd use SmtpClient or a library like MailKit
        return Task.CompletedTask;
    }
}
