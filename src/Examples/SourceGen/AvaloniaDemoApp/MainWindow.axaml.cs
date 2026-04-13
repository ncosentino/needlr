using Avalonia.Controls;
using Avalonia.Interactivity;
using NexusLabs.Needlr.Avalonia;

namespace AvaloniaDemoApp;

/// <summary>
/// Main window with constructor-injected <see cref="GreetingService"/>.
///
/// <see cref="GenerateAvaloniaDesignTimeConstructorAttribute"/> tells the
/// Needlr Avalonia generator to emit a parameterless constructor with a
/// <c>Design.IsDesignMode</c> guard and <c>InitializeComponent()</c> for
/// the XAML previewer. At runtime, Needlr's richest-constructor selection
/// picks the DI constructor below.
///
/// No hand-written boilerplate, no pragma suppression, no copy-paste —
/// the generator handles it all.
/// </summary>
[GenerateAvaloniaDesignTimeConstructor]
public sealed partial class MainWindow : Window
{
    private readonly GreetingService _greetingService;
    private int _clickCount;

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
