using Microsoft.Extensions.DependencyInjection;
using NexusLabs.Needlr;

namespace MultiProjectApp.Features.Notifications;

/// <summary>Sends notifications to recipients.</summary>
public interface INotificationService
{
    void Send(string recipient, string message);
}

/// <summary>In-memory stub implementation of <see cref="INotificationService"/>.</summary>
public sealed class InMemoryNotificationService : INotificationService
{
    public void Send(string recipient, string message) =>
        Console.WriteLine($"[Notification] To: {recipient} | {message}");
}

/// <summary>
/// Registers notification services into the DI container.
/// </summary>
public sealed class NotificationsPlugin : IServiceCollectionPlugin
{
    public void Configure(ServiceCollectionPluginOptions options)
    {
        options.Services.AddSingleton<INotificationService, InMemoryNotificationService>();
    }
}
