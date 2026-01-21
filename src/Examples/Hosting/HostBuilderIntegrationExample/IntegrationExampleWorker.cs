using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HostBuilderIntegrationExample;

/// <summary>
/// Example worker that runs in the integration example.
/// Note: This is registered manually, not via Needlr auto-discovery.
/// </summary>
public sealed class IntegrationExampleWorker : BackgroundService
{
    private readonly ILogger<IntegrationExampleWorker> _logger;
    private readonly ICustomService _customService;
    private readonly IAutoDiscoveredService _autoDiscoveredService;

    public IntegrationExampleWorker(
        ILogger<IntegrationExampleWorker> logger,
        ICustomService customService,
        IAutoDiscoveredService autoDiscoveredService)
    {
        _logger = logger;
        _customService = customService;
        _autoDiscoveredService = autoDiscoveredService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("IntegrationExampleWorker starting at: {Time}", DateTimeOffset.Now);

        while (!stoppingToken.IsCancellationRequested)
        {
            // Use manually registered service
            var customMessage = _customService.GetMessage();
            _logger.LogInformation("[Custom] {Message}", customMessage);
            
            // Use Needlr auto-discovered service
            var autoMessage = _autoDiscoveredService.GetAutoMessage();
            _logger.LogInformation("[AutoDiscovered] {Message}", autoMessage);

            try
            {
                await Task.Delay(3000, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("IntegrationExampleWorker stopping at: {Time}", DateTimeOffset.Now);
    }
}
