using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;

namespace AvaloniaDemoApp;

/// <summary>
/// Entry point demonstrating Needlr source generation with Avalonia.
///
/// Key points:
/// - NeedlrExcludeNamespacePrefix=Avalonia in the .csproj prevents Needlr from scanning
///   Avalonia's hundreds of framework types while still auto-registering app types.
/// - Full AOT + trimming compatible via NexusLabs.Needlr.Build source generation.
/// - Services are resolved from the Needlr-built DI container and passed to Avalonia views.
/// </summary>
internal static class Program
{
    // The DI container is built once and shared with the Avalonia app.
    internal static IServiceProvider? Services { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        // Build the Needlr DI container with source generation.
        // Avalonia types are excluded via NeedlrExcludeNamespacePrefix in the .csproj —
        // only AvaloniaDemoApp.* types are scanned and registered.
        Services = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
