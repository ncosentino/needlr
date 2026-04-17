---
applyTo: "**/*ViewModel.cs"
---

# ViewModel Rules

## Colocation

A ViewModel lives beside its corresponding view or control as a **sibling file**:

```
Features/Scan/
  ChooseView.axaml
  ChooseView.axaml.cs
  ChooseViewModel.cs
```

Do NOT place ViewModels in a separate `ViewModels/` folder away from their views.

**Migration note:** When editing existing features that use separate `Views/` and `ViewModels/` folders, preserve the existing folder structure unless the task explicitly requests migration. New UI work must use sibling colocation.

## CommunityToolkit.Mvvm

Prefer CommunityToolkit.Mvvm for all ViewModel infrastructure:

- Inherit from `ObservableObject`
- Use `[ObservableProperty]` for bindable properties
- Use `[RelayCommand]` for commands
- Use `[NotifyPropertyChangedFor]` and `[NotifyCanExecuteChangedFor]` for dependent property/command invalidation

```csharp
public partial class ChooseViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNewSelected))]
    [NotifyCanExecuteChangedFor(nameof(GoForwardCommand))]
    private string? _selectedOption = "new";

    public bool IsNewSelected => SelectedOption == "new";

    [RelayCommand(CanExecute = nameof(CanGoForward))]
    private void GoForward() { /* ... */ }

    private bool CanGoForward() => !string.IsNullOrEmpty(SelectedOption);
}
```

## String resources — exposed through properties

Views NEVER access `.resx` resource classes directly. All user-visible strings are exposed as ViewModel properties:

```csharp
// ✅ CORRECT — ViewModel exposes localized strings
public string TitleText => Strings.Choose_Title;
public string BackButtonText => ShellStrings.Shared_BackButton;
public string NextButtonText => ShellStrings.Shared_NextButton;
```

```xml
<!-- ✅ CORRECT — View binds to ViewModel property -->
<TextBlock Text="{Binding TitleText}" />

<!-- ❌ WRONG — View accesses resource class directly -->
<TextBlock Text="{x:Static res:Strings.Choose_Title}" />
```

This ensures all localized text flows through a single consistent layer and can be tested, overridden, or dynamically switched via the ViewModel.

## Dependency injection

ViewModels receive their dependencies through constructor injection. Use explicit constructors — not primary constructors — for consistency with code-behind classes:

```csharp
public partial class ChooseViewModel : ObservableObject
{
    private readonly INavigationService _nav;
    private readonly IScanContext _scanContext;

    public ChooseViewModel(
        INavigationService nav,
        IScanContext scanContext)
    {
        _nav = nav;
        _scanContext = scanContext;
    }
}
```

## Scope of responsibility

ViewModels are the bridge between the view and the application logic:

- **YES:** expose bindable properties, commands, localized strings, and computed display state
- **YES:** delegate to injected services for actual business logic
- **NO:** direct database access, HTTP calls, or file I/O — these belong in services
- **NO:** knowledge of view types, XAML elements, or UI framework classes
