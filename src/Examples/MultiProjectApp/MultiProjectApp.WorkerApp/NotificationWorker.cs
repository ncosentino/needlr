using Microsoft.Extensions.Hosting;
using MultiProjectApp.Features.Notifications;

namespace MultiProjectApp.WorkerApp;

/// <summary>A background worker that periodically sends a notification.</summary>
public sealed class NotificationWorker : BackgroundService
{
    private readonly INotificationService _notifications;

    public NotificationWorker(INotificationService notifications)
    {
        _notifications = notifications;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _notifications.Send("ops@example.com", $"Worker heartbeat at {DateTime.UtcNow:O}");
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
