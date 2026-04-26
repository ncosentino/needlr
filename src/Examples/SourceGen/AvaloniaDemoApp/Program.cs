using Avalonia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;

namespace AvaloniaDemoApp;

/// <summary>
/// Entry point demonstrating constructor-injected Avalonia views with Needlr.
///
/// The service provider is built ONCE, passed to the App instance via Avalonia's
/// AfterSetup callback, and the App resolves MainWindow from DI — which auto-injects
/// GreetingService into MainWindow's constructor. No static service locator, no
/// manual GetRequiredService calls in view code.
/// </summary>
internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Build configuration from appsettings.json so that [Options] and
        // [HttpClientOptions] bindings can read their configuration sections.
        // The parameterless BuildServiceProvider() overload uses an EMPTY
        // configuration — it will NOT discover appsettings.json automatically.
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        // Build the Needlr DI container. Source generation discovers GreetingService
        // and MainWindow automatically; Avalonia types are excluded via
        // NeedlrExcludeNamespacePrefix in the .csproj.
        var services = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider(configuration);

        BuildAvaloniaApp()
            .AfterSetup(builder =>
            {
                // Pass the service provider to the App instance AFTER Avalonia creates
                // it but BEFORE the lifetime starts. This is the single bridge point
                // between Needlr's DI and Avalonia's bootstrap — no static required.
                if (builder.Instance is App app)
                {
                    app.Services = services;
                }
            })
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
