using Microsoft.Extensions.Options;

using NexusLabs.Needlr;

using OptionsValidationExample.Options;

namespace OptionsValidationExample.Services;

/// <summary>
/// Example service consuming API client options.
/// </summary>
public interface IExternalApiClient
{
    Task<string> GetDataAsync(string endpoint);
}

[RegisterAs<IExternalApiClient>]
public sealed class ExternalApiClient : IExternalApiClient
{
    private readonly ApiClientOptions _options;
    private readonly ILogger<ExternalApiClient> _logger;

    public ExternalApiClient(IOptions<ApiClientOptions> options, ILogger<ExternalApiClient> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task<string> GetDataAsync(string endpoint)
    {
        _logger.LogInformation(
            "Calling {BaseUrl}/{Endpoint} with timeout {Timeout}s",
            _options.BaseUrl,
            endpoint,
            _options.TimeoutSeconds);

        // In a real app, you'd use HttpClient
        return Task.FromResult($"Data from {_options.BaseUrl}/{endpoint}");
    }
}
