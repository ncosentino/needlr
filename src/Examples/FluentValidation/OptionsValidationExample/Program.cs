using Microsoft.Extensions.Options;

using NexusLabs.Needlr.AspNet;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;

using OptionsValidationExample.Options;
using OptionsValidationExample.Services;

var app = new Syringe()
    .UsingSourceGen()
    .BuildWebApplication();

// Map endpoints to demonstrate options usage
app.MapGet("/", () => Results.Ok(new
{
    Message = "FluentValidation Options Example",
    Endpoints = new[]
    {
        "GET /config - View current configuration",
        "GET /send-email - Test email service",
        "GET /api-call - Test API client"
    }
}));

app.MapGet("/config", (
    IOptions<DatabaseOptions> dbOptions,
    IOptions<SmtpOptions> smtpOptions,
    IOptions<ApiClientOptions> apiOptions) =>
{
    return Results.Ok(new
    {
        Database = new
        {
            dbOptions.Value.ConnectionString,
            dbOptions.Value.MaxPoolSize,
            dbOptions.Value.CommandTimeoutSeconds,
            dbOptions.Value.EnableRetryOnFailure
        },
        Smtp = new
        {
            smtpOptions.Value.Host,
            smtpOptions.Value.Port,
            smtpOptions.Value.FromAddress,
            smtpOptions.Value.UseSsl
        },
        ApiClient = new
        {
            apiOptions.Value.BaseUrl,
            apiOptions.Value.TimeoutSeconds,
            apiOptions.Value.MaxRetries
        }
    });
});

app.MapGet("/send-email", async (IEmailService emailService) =>
{
    await emailService.SendEmailAsync("test@example.com", "Test Subject", "Test Body");
    return Results.Ok("Email sent (simulated)");
});

app.MapGet("/api-call", async (IExternalApiClient apiClient) =>
{
    var result = await apiClient.GetDataAsync("users");
    return Results.Ok(new { Result = result });
});

app.Run();
