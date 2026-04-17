---
# AUTO-GENERATED from .github/instructions/genesis/ui/app-shell.instructions.md — do not edit
paths:
  - "**/App.axaml"
  - "**/App.xaml"
  - "**/App.axaml.cs"
  - "**/App.xaml.cs"
---
# Application Shell Rules (`App.xaml` / `App.axaml`)

Application-level files are the composition root and global resource entry point. They follow different rules than views and controls.

## App markup — resource merging only

`App.xaml` / `App.axaml` markup is limited to:

- Merging global theme resource dictionaries
- Declaring application-wide resources (e.g., converters, view locators)
- Setting the base theme (e.g., Fluent, Material)

```xml
<!-- Avalonia example -->
<Application.Styles>
  <FluentTheme />
  <StyleInclude Source="/Themes/AppTheme.axaml" />
</Application.Styles>
```

```xml
<!-- WPF example -->
<Application.Resources>
  <ResourceDictionary>
    <ResourceDictionary.MergedDictionaries>
      <ResourceDictionary Source="/Themes/AppTheme.xaml" />
    </ResourceDictionary.MergedDictionaries>
  </ResourceDictionary>
</Application.Resources>
```

**FORBIDDEN in App markup:**

- Feature-specific view layout or content
- Data bindings to a ViewModel
- Hard-coded user-visible strings

## App code-behind — startup and lifetime only

`App.xaml.cs` / `App.axaml.cs` may:

- Configure the DI container and host
- Resolve the root window/view from DI
- Handle application lifecycle events (startup, shutdown, unhandled exceptions)
- Initialize logging and telemetry

**FORBIDDEN in App code-behind:**

- Feature-specific business logic
- Direct instantiation of views without DI (`new MainWindow()` is acceptable only if MainWindow itself resolves its ViewModel from DI)
- Navigation logic — this belongs in a navigation service

## WPF-specific: no `StartupUri`

`StartupUri` in `App.xaml` bypasses the DI container. The main window must be resolved from the service provider in `App.xaml.cs`.
