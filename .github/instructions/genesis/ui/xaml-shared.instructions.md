---
applyTo: "**/*xaml,**/*xaml.cs"
---

# Shared XAML & Code-Behind Rules

These rules apply to **view and control** XAML files and their code-behind in both WPF (`.xaml`) and Avalonia (`.axaml`). Theme resource dictionaries and application-level files (`App.xaml` / `App.axaml`) have their own specific instructions that take precedence.

## XAML markup — declarative layout only

XAML files are purely declarative. They define layout, data bindings, and resource references — nothing else.

**FORBIDDEN in XAML markup:**

- Inline event handlers (e.g., `Click="OnButtonClick"`) — use `Command` bindings to the ViewModel instead
- Hard-coded user-visible strings — all text binds to ViewModel properties that return localized strings from `.resx` resources
- Inline visual styling — colors, brushes, fonts, corner radii, and spacing tokens come from theme `StaticResource` keys, never set directly on individual controls
- Business logic of any kind — no converters with logic, no multi-binding tricks to compute values

```xml
<!-- ❌ WRONG — hard-coded string, inline color -->
<TextBlock Text="Welcome back!" Foreground="#1FA79B" />

<!-- ✅ CORRECT — bound to ViewModel, themed -->
<TextBlock Text="{Binding WelcomeText}" Foreground="{StaticResource TealBrush}" />
```

### `StaticResource` vs `DynamicResource`

Use `StaticResource` by default for all theme tokens. Use `DynamicResource` only when runtime theme switching is required (e.g., light/dark mode toggle). Layout-structural values that are local to the view (e.g., a specific `Grid.RowDefinitions` split) are allowed inline — they are layout, not visual styling.

## Code-behind — thin DI shell

Code-behind files exist solely to:

1. Receive the ViewModel via constructor injection
2. Assign it to `DataContext`
3. Call `InitializeComponent()`

```csharp
public partial class ChooseView : UserControl
{
    public ChooseView(ChooseViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
```

**FORBIDDEN in code-behind:**

- Business logic or data transformation
- Direct manipulation of UI elements (e.g., `myTextBox.Text = "..."`)
- Event handler methods — use ViewModel commands
- Service calls or repository access

The only acceptable additions beyond the constructor are framework-required overrides (e.g., `OnClosing` for cleanup) that delegate immediately to the ViewModel.

## View resolution via dependency injection

All top-level views and their ViewModels are resolved through the DI container — never `new`'d manually. This means:

- Child views that require their own ViewModel are resolved via DI, not declared inline in parent XAML
- Parent ViewModels may expose child ViewModels as properties, with the parent view using `ContentControl` + `DataTemplate` or a view-locator pattern to display them
- `DataTemplate`-driven composition and view-locator patterns are acceptable for content switching

```xml
<!-- ✅ CORRECT — parent exposes child VM, template resolves the view -->
<ContentControl Content="{Binding CurrentPageViewModel}" />

<!-- ❌ WRONG — directly nesting a view that needs its own DI-injected ViewModel -->
<local:ChildView />
```

Simple controls that do NOT require a ViewModel (e.g., a reusable `IconButton` control with only dependency properties) may be used directly in XAML.

## View + ViewModel colocation

A view, its code-behind, and its primary ViewModel are **sibling files** in the same folder:

```
Features/Scan/
  ChooseView.axaml
  ChooseView.axaml.cs
  ChooseViewModel.cs
```

Do NOT split into separate `Views/` and `ViewModels/` folders:

```
❌ Features/Scan/Views/ChooseView.axaml
❌ Features/Scan/ViewModels/ChooseViewModel.cs
```

**Migration note:** When editing existing features that use separate `Views/` and `ViewModels/` folders, preserve the existing folder structure unless the task explicitly requests migration. New UI work must use sibling colocation.
