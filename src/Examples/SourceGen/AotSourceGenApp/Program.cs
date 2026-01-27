using AotSourceGenPlugin;

using Microsoft.AspNetCore.Mvc;

using NexusLabs.Needlr.AspNet;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;

using System.Text.Json.Serialization;

// Decorators are automatically applied via [DecoratorFor<T>] attributes in AotSourceGenPlugin
// See: LoggingWeatherDecorator (Order=1) and PrefixWeatherDecorator (Order=2)
//
// Open Generic Decorators are also demonstrated via [OpenDecoratorFor(typeof(IMessageHandler<>))]
// These automatically decorate ALL closed implementations of IMessageHandler<T>
// Endpoints: /order/{id} and /notify/{message}
var app = new Syringe()
    .UsingSourceGen()
    .ForWebApplication()
    .UsingOptions(() => CreateWebApplicationOptions.Default.UsingCliArgs(args))
    .BuildWebApplication();

app.MapGet("/weather", static ([FromServices] IWeatherProvider weather) => Results.Text(weather.GetForecast()));
app.MapGet("/time", static ([FromServices] ITimeProvider time) => Results.Text(time.GetNow().ToString("O")));
app.MapGet("/manual/{value}", static ([FromServices] IManualService manual, string value) => Results.Text(manual.Echo(value)));
app.MapGet("/all", static ([FromServices] IWeatherProvider weather, [FromServices] ITimeProvider time, [FromServices] IManualService manual) =>
    Results.Json(
        new AllResponse(
            Weather: weather.GetForecast(),
            Time: time.GetNow(),
            Manual: manual.Echo("hi")),
        AppJsonContext.Default.AllResponse));

Console.WriteLine("AotSourceGenApp running. Reflection disabled; demonstrating source-gen parity.");
Console.WriteLine("Endpoints: /weather, /time, /manual/{value}, /all, /plugin-weather, /plugin-time, /plugin-manual/{value}");
Console.WriteLine("Open Generic Decorator endpoints: /order/{id}, /notify/{message}");

await app.RunAsync();

internal sealed record AllResponse(string Weather, DateTimeOffset Time, string Manual);

[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(AllResponse))]
internal sealed partial class AppJsonContext : JsonSerializerContext;
