using System.ComponentModel;
using System.Windows.Input;

namespace NeedlrMauiExampleApp;

/// <summary>
/// View model for <see cref="MainPage"/>. Needlr discovers and registers this type automatically and
/// injects it into the page's constructor. It depends on <see cref="IGreetingService"/>, which Needlr
/// also injects — demonstrating layered DI (service → view model → view) with no manual registration.
/// </summary>
public sealed class MainPageViewModel : INotifyPropertyChanged
{
    private int _clickCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainPageViewModel"/> class.
    /// </summary>
    /// <param name="greetingService">The greeting service, injected by Needlr.</param>
    public MainPageViewModel(IGreetingService greetingService)
    {
        Greeting = greetingService.Greet();
        IncrementCommand = new Command(Increment);
    }

    /// <summary>The greeting produced by the injected service. Bound by the view.</summary>
    public string Greeting { get; }

    /// <summary>Text for the counter button. Bound by the view.</summary>
    public string CounterText => _clickCount switch
    {
        0 => "Click me",
        1 => "Clicked 1 time",
        _ => $"Clicked {_clickCount} times",
    };

    /// <summary>Command bound to the counter button.</summary>
    public ICommand IncrementCommand { get; }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Increment()
    {
        _clickCount++;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CounterText)));
    }
}
