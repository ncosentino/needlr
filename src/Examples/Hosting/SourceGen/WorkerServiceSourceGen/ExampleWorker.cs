using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using NexusLabs.Needlr;

namespace WorkerServiceSourceGen;

/// <summary>
/// Example background worker that demonstrates IHostedService auto-discovery with source generation.
/// Needlr automatically discovers and registers this as a singleton, and the
/// host infrastructure starts it automatically.
/// </summary>
public sealed class ExampleWorker : BackgroundService
{
    private readonly ILogger<ExampleWorker> _logger;
    private readonly IExampleService _exampleService;

    public ExampleWorker(ILogger<ExampleWorker> logger, IExampleService exampleService)
    {
        _logger = logger;
        _exampleService = exampleService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ExampleWorker (SourceGen) starting at: {Time}", DateTimeOffset.Now);

        while (!stoppingToken.IsCancellationRequested)
        {
            var message = _exampleService.GetMessage();
            _logger.LogInformation("[{Time}] {Message}", DateTimeOffset.Now, message);

            try
            {
                await Task.Delay(3000, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("ExampleWorker (SourceGen) stopping at: {Time}", DateTimeOffset.Now);
    }
}
