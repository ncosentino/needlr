namespace NeedlrMauiExampleApp;

public partial class App : Application
{
	private readonly Lazy<MainPage> _mainPage;

	// Needlr automatically registers Lazy<T> for every discovered service, so the page is
	// constructor-injected here and created only when the window is first shown — real DI,
	// no service locator and no manual page registration.
	public App(Lazy<MainPage> mainPage)
	{
		InitializeComponent();
		_mainPage = mainPage;
	}

	protected override Window CreateWindow(IActivationState? activationState)
		=> new Window(_mainPage.Value);
}