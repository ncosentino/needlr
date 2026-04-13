using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AvaloniaDemoApp;

/// <summary>
/// Main window with constructor-injected <see cref="GreetingService"/>.
///
/// Needlr's source generator discovers this class and registers it in DI.
/// When <see cref="App.OnFrameworkInitializationCompleted"/> resolves
/// <c>MainWindow</c> from the container, Needlr sees that the constructor
/// requires <see cref="GreetingService"/> and injects it automatically.
///
/// No service locator, no <c>GetRequiredService</c> in view code —
/// pure constructor injection, same pattern as ASP.NET Core controllers.
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly GreetingService _greetingService;
    private int _clickCount;

    /// <summary>
    /// Parameterless constructor for Avalonia's XAML loader and design-time previewer.
    /// At runtime, the DI container uses the richer constructor below.
    /// </summary>
    public MainWindow() : this(new GreetingService()) { }

    /// <summary>
    /// DI constructor — Needlr picks this over the parameterless constructor because
    /// it has more satisfiable parameters (richest constructor wins, matching
    /// ActivatorUtilities behavior).
    /// </summary>
    public MainWindow(GreetingService greetingService)
    {
        _greetingService = greetingService;
        InitializeComponent();
        ServiceMessage.Text = _greetingService.GetWelcomeMessage();
    }

    private void OnGreetClicked(object? sender, RoutedEventArgs e)
    {
        _clickCount++;
        ServiceMessage.Text = _greetingService.GetGreeting(_clickCount);
        CounterText.Text = $"Clicks: {_clickCount}";
    }
}
