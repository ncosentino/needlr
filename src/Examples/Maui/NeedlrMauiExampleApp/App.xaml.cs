using Microsoft.Extensions.DependencyInjection;

namespace NeedlrMauiExampleApp;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		// Resolve the first page from the Needlr-populated container so its constructor
		// dependencies (IGreetingService) are injected — no manual page registration required.
		var services = activationState?.Context.Services
			?? IPlatformApplication.Current!.Services;
		return new Window(services.GetRequiredService<MainPage>());
	}
}