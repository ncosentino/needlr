using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using NexusLabs.Needlr;

namespace AvaloniaDemoApp;

/// <summary>
/// Avalonia application that resolves the main window from Needlr's DI container.
/// The <see cref="Services"/> property is set by <see cref="Program.Main"/> via
/// Avalonia's <c>AfterSetup</c> callback — no static service locator needed.
/// </summary>
[DoNotAutoRegister] // App is created by Avalonia, not by DI.
public sealed class App : Application
{
    /// <summary>
    /// The Needlr service provider, set by Program.Main before the lifetime starts.
    /// </summary>
    internal IServiceProvider? Services { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Resolve MainWindow from DI — GreetingService is constructor-injected
            // automatically by the container. No manual GetRequiredService calls
            // in view code.
            desktop.MainWindow = Services!.GetRequiredService<MainWindow>();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
