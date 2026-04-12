using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;

namespace AvaloniaDemoApp;

/// <summary>
/// Main window that resolves services from Needlr's DI container.
/// Demonstrates that Needlr source-gen correctly auto-registered
/// <see cref="GreetingService"/> while excluding all Avalonia types.
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly GreetingService _greetingService;
    private int _clickCount;

    public MainWindow()
    {
        InitializeComponent();

        // Resolve the Needlr auto-registered service from the DI container.
        // This service was discovered at compile time by the source generator
        // because its namespace (AvaloniaDemoApp) matches the include filter,
        // while Avalonia.* types were excluded via NeedlrExcludeNamespacePrefix.
        _greetingService = Program.Services!.GetRequiredService<GreetingService>();

        ServiceMessage.Text = _greetingService.GetWelcomeMessage();
    }

    private void OnGreetClicked(object? sender, RoutedEventArgs e)
    {
        _clickCount++;
        ServiceMessage.Text = _greetingService.GetGreeting(_clickCount);
        CounterText.Text = $"Clicks: {_clickCount}";
    }
}
