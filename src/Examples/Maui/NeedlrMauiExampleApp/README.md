# Needlr .NET MAUI example

A minimal .NET MAUI head that uses **Needlr source-generated dependency injection** end to end.

## What it demonstrates

- `MauiProgram.CreateMauiApp` calls `builder.UseNeedlr(s => s.UsingSourceGen())` — one line wires
  every Needlr-discovered service into MAUI's single container.
- Needlr's source generator runs on the head (`NeedlrAutoGenerate=true`) and registers `App`,
  `MainPage`, `MainPageViewModel`, and `GreetingService` with **no manual registration**. The
  per-platform application entry points under `Platforms/` (Windows `App : MauiWinUIApplication`,
  Android `MainApplication : MauiApplication`) are skipped automatically.
- **MVVM via DI (the core pattern):** `MainPageViewModel` — which itself depends on
  `IGreetingService` — is injected into `MainPage`'s constructor and assigned to `BindingContext`;
  the XAML binds to it. Your view models flow into your views' constructors with zero manual
  registration.
- `App` receives a `Lazy<MainPage>` by **constructor injection** (Needlr automatically registers
  `Lazy<T>` for every service) and creates the page when the window first shows — deferred creation
  with real DI, no service locator.

## Why it is not in the solution

A MAUI head needs the MAUI workload and platform target frameworks, so this project is **excluded
from `NexusLabs.Needlr.slnx` and the workload-free CI build**. It builds against the **local** Needlr
source (project references), making it a regression guard for source-gen-on-a-MAUI-head and the
`NexusLabs.Needlr.Maui` integration.

## Building it

Install the MAUI workload, then build a platform target:

```bash
dotnet workload install maui-android maui-windows

# Windows (requires Windows)
dotnet build src/Examples/Maui/NeedlrMauiExampleApp/NeedlrMauiExampleApp.csproj -f net10.0-windows10.0.19041.0

# Android
dotnet build src/Examples/Maui/NeedlrMauiExampleApp/NeedlrMauiExampleApp.csproj -f net10.0-android
```

Or use the helper script (Windows + Android by default):

```powershell
./scripts/build-maui-example.ps1
```

CI builds this example on demand and on MAUI-related changes via the **MAUI Example** workflow
(`.github/workflows/maui-example.yml`).
