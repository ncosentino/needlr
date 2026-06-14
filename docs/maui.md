# .NET MAUI

`NexusLabs.Needlr.Maui` integrates Needlr's source-generated dependency injection into a .NET MAUI
application. Your `App`, pages, and view models — plus every Needlr-discovered service across your
solution — resolve from MAUI's single built-in container, with no per-type manual registration.

## Quick Start

Install the package in your MAUI head project:

```xml
<PackageReference Include="NexusLabs.Needlr.Maui" />
<PackageReference Include="NexusLabs.Needlr.Injection.SourceGen" />
```

Wire Needlr into your `MauiProgram`:

```csharp
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;
using NexusLabs.Needlr.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseNeedlr(syringe => syringe.UsingSourceGen());

        return builder.Build();
    }
}
```

That single `UseNeedlr` call applies every Needlr-discovered registration to `builder.Services`.
MAUI then builds and owns the resulting `IServiceProvider`, so pages and view models are constructor
-injected the same way MAUI injects any DI-registered type.

## One container, populated by Needlr

MAUI creates exactly one DI container via `MauiApp.CreateBuilder()`. Rather than building a separate
Needlr provider, the MAUI integration **populates MAUI's container**: it calls Needlr's
`IServiceCollectionPopulator` against `builder.Services`, using `builder.Configuration` for
`[Options]`/`[HttpClientOptions]` binding — the same flow the ASP.NET and host integrations use.

There are two equivalent entry points:

```csharp
// One-liner on the builder
builder.UseNeedlr(syringe => syringe.UsingSourceGen());

// Or the fluent form, if you already have a ConfiguredSyringe
new Syringe()
    .UsingSourceGen()
    .ForMaui()
    .PopulateInto(builder);
```

Both preserve anything already registered on the builder (MAUI's own services, and your own
`builder.Services.Add...` calls).

## Source generation on the MAUI head

Needlr's source generator runs on the MAUI head project like any other assembly. When
`NeedlrAutoGenerate=true` (the default when you reference `NexusLabs.Needlr.Build`), it scans the
head and registers your `App`, pages, views, and view models automatically.

The generator **skips the per-platform application entry points** — the Windows
`App : MauiWinUIApplication`, the Android `MainApplication : MauiApplication`, and the iOS/Mac
`AppDelegate : MauiUIApplicationDelegate` under `Platforms/`. These are framework-owned objects that
MAUI constructs itself and that the platform toolchains (WinRT/CsWinRT, Android) decorate with
interop members which are not accessible from generated code. They are never resolved as services,
so excluding them is both correct and necessary — without it the head would fail to compile.

Everything you actually author remains scannable:

| Type | Scanned & registered |
|------|----------------------|
| Cross-platform `App : Application`, `AppShell : Shell` | Yes |
| Pages (`ContentPage`), views (`ContentView`), custom controls | Yes |
| View models and services | Yes |
| Windows `*.WinUI.App`, Android `MainApplication`, iOS/Mac `AppDelegate` | No (framework entry points) |

If you prefer to keep the head free of source generation entirely, set `NeedlrAutoGenerate=false` on
the head and put your injectable types in a referenced class library; `UseNeedlr` will still populate
MAUI's container from those generated libraries.

## View models and pages

Because the head is scanned, your pages and view models are registered automatically. Inject a view
model into a page's constructor and assign it to `BindingContext` — the canonical MVVM wiring, with
no manual registration:

```csharp
public partial class MainPage : ContentPage
{
    public MainPage(MainPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
```

Needlr also registers `Lazy<T>` (and `IReadOnlyList<T>`) for every service, so you can defer creation
where MAUI needs it — for example injecting `Lazy<MainPage>` into `App` and creating the page when the
window is first shown, instead of reaching for a service locator:

```csharp
public partial class App : Application
{
    private readonly Lazy<MainPage> _mainPage;

    public App(Lazy<MainPage> mainPage)
    {
        InitializeComponent();
        _mainPage = mainPage;
    }

    protected override Window CreateWindow(IActivationState? activationState)
        => new Window(_mainPage.Value);
}
```

## API reference

| Member | Description |
|--------|-------------|
| `ConfiguredSyringe.ForMaui()` | Transitions a configured syringe into a `MauiSyringe`. |
| `MauiSyringe.PopulateInto(MauiAppBuilder)` | Applies the discovered registrations to the builder's service collection and returns the builder. |
| `MauiAppBuilder.UseNeedlr(ConfiguredSyringe)` | Populates the builder from an already-configured syringe. |
| `MauiAppBuilder.UseNeedlr(Func<Syringe, ConfiguredSyringe>)` | Populates the builder, choosing the discovery strategy inline (e.g. `s => s.UsingSourceGen()`). |

## Notes

- The `NexusLabs.Needlr.Maui` package targets `net10.0` and references `Microsoft.Maui.Controls`. It
  builds and unit-tests without the MAUI workload — the workload is only needed to build the app head
  itself.
- Use `UsingSourceGen()` for AOT/trimming-friendly, zero-reflection discovery (recommended for
  mobile). `UsingReflection()` is also supported if you reference `NexusLabs.Needlr.Injection.Reflection`.
