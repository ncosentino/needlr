using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;
using NexusLabs.Needlr.Maui;

namespace NeedlrMauiExampleApp;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			})
			// Populate MAUI's single container with every Needlr-discovered service — the App, pages,
			// and IGreetingService — using source generation. No per-type manual registration.
			.UseNeedlr(syringe => syringe.UsingSourceGen());

		return builder.Build();
	}
}
