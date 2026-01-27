using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr;
using NexusLabs.Needlr.AspNet;

namespace AotSourceGenPlugin;

public interface IWeatherProvider
{
    string GetForecast();
}

public sealed class WeatherProvider : IWeatherProvider
{
    public string GetForecast() => "Sunny with AOT";
}

public interface ITimeProvider
{
    DateTimeOffset GetNow();
}

public sealed class TimeProvider : ITimeProvider
{
    public DateTimeOffset GetNow() => DateTimeOffset.UtcNow;
}

[DoNotAutoRegister]
public interface IManualService
{
    string Echo(string value);
}

public sealed class ManualService : IManualService
{
    public string Echo(string value) => $"manual:{value}";
}

/// <summary>
/// Decorator using [DecoratorFor] attribute - automatically wired by Needlr.
/// Order = 1 means this is applied first (closest to the original WeatherProvider).
/// </summary>
[DecoratorFor<IWeatherProvider>(Order = 1)]
public sealed class LoggingWeatherDecorator(IWeatherProvider inner) : IWeatherProvider
{
    public string GetForecast()
    {
        Console.WriteLine("[LoggingWeatherDecorator] Fetching forecast...");
        return inner.GetForecast();
    }
}

/// <summary>
/// Second decorator using [DecoratorFor] attribute.
/// Order = 2 means this wraps LoggingWeatherDecorator.
/// Resolution chain: PrefixWeatherDecorator → LoggingWeatherDecorator → WeatherProvider
/// </summary>
[DecoratorFor<IWeatherProvider>(Order = 2)]
public sealed class PrefixWeatherDecorator(IWeatherProvider inner) : IWeatherProvider
{
    public string GetForecast() => $"[decorated] {inner.GetForecast()}";
}

// Manual registration via IServiceCollectionPlugin
public sealed class ManualRegistrationPlugin : IServiceCollectionPlugin
{
    public void Configure(ServiceCollectionPluginOptions options)
    {
        options.Services.AddSingleton<IManualService, ManualService>();
    }
}

// Application-level plugin (web endpoints)
public sealed class WeatherPlugin : IWebApplicationPlugin
{
    public void Configure(WebApplicationPluginOptions options)
    {
        options.WebApplication.MapGet("/plugin-weather", static ([FromServices] IWeatherProvider weather) => Results.Text(weather.GetForecast()));
        options.WebApplication.MapGet("/plugin-time", static ([FromServices] ITimeProvider time) => Results.Text(time.GetNow().ToString("O")));
        options.WebApplication.MapGet("/plugin-manual/{value}", static ([FromServices] IManualService manual, string value) => Results.Text(manual.Echo(value)));
    }
}

// Post-build plugin for runtime verification
public sealed class StartupPlugin : IPostBuildServiceCollectionPlugin
{
    public void Configure(PostBuildServiceCollectionPluginOptions options)
    {
        var manual = options.Provider.GetRequiredService<IManualService>();
        Console.WriteLine($"StartupPlugin manual={manual.Echo("hi")}");
    }
}

#region Open Generic Decorator Example

/// <summary>
/// Generic handler interface for demonstrating [OpenDecoratorFor] feature.
/// All closed implementations will be automatically decorated.
/// </summary>
public interface IMessageHandler<T>
{
    string Handle(T message);
}

/// <summary>
/// Message type for order-related operations.
/// </summary>
public sealed class OrderMessage
{
    public string OrderId { get; init; } = string.Empty;
}

/// <summary>
/// Message type for notification-related operations.
/// </summary>
public sealed class NotificationMessage
{
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Concrete handler for OrderMessage.
/// </summary>
public sealed class OrderMessageHandler : IMessageHandler<OrderMessage>
{
    public string Handle(OrderMessage message) => $"Processing order {message.OrderId}";
}

/// <summary>
/// Concrete handler for NotificationMessage.
/// </summary>
public sealed class NotificationMessageHandler : IMessageHandler<NotificationMessage>
{
    public string Handle(NotificationMessage message) => $"Sending notification: {message.Message}";
}

/// <summary>
/// Open generic decorator that automatically decorates ALL IMessageHandler implementations.
/// Using [OpenDecoratorFor] instead of [DecoratorFor&lt;T&gt;] enables decorating all closed generics at compile time.
/// This decorator will wrap both OrderMessageHandler and NotificationMessageHandler.
/// </summary>
[NexusLabs.Needlr.Generators.OpenDecoratorFor(typeof(IMessageHandler<>), Order = 1)]
public sealed class LoggingMessageDecorator<T> : IMessageHandler<T>
{
    private readonly IMessageHandler<T> _inner;

    public LoggingMessageDecorator(IMessageHandler<T> inner)
    {
        _inner = inner;
    }

    public string Handle(T message)
    {
        Console.WriteLine($"[LoggingMessageDecorator<{typeof(T).Name}>] Handling message...");
        return _inner.Handle(message);
    }
}

/// <summary>
/// Second open generic decorator wrapping the logging decorator.
/// Order = 2 means this wraps LoggingMessageDecorator.
/// </summary>
[NexusLabs.Needlr.Generators.OpenDecoratorFor(typeof(IMessageHandler<>), Order = 2)]
public sealed class MetricsMessageDecorator<T> : IMessageHandler<T>
{
    private readonly IMessageHandler<T> _inner;

    public MetricsMessageDecorator(IMessageHandler<T> inner)
    {
        _inner = inner;
    }

    public string Handle(T message)
    {
        var start = DateTime.UtcNow;
        var result = _inner.Handle(message);
        var elapsed = DateTime.UtcNow - start;
        Console.WriteLine($"[MetricsMessageDecorator<{typeof(T).Name}>] Completed in {elapsed.TotalMilliseconds:F2}ms");
        return result;
    }
}

/// <summary>
/// Plugin that adds endpoints to demonstrate open generic decorators.
/// </summary>
public sealed class MessageHandlerPlugin : IWebApplicationPlugin
{
    public void Configure(WebApplicationPluginOptions options)
    {
        options.WebApplication.MapGet("/order/{id}", ([FromServices] IMessageHandler<OrderMessage> handler, string id) => 
            Results.Text(handler.Handle(new OrderMessage { OrderId = id })));
        
        options.WebApplication.MapGet("/notify/{message}", ([FromServices] IMessageHandler<NotificationMessage> handler, string message) =>
            Results.Text(handler.Handle(new NotificationMessage { Message = message })));
    }
}

#endregion
