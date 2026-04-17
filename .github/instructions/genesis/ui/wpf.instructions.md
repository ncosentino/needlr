---
applyTo: "**/*.xaml,**/*.xaml.cs"
---

# WPF-Specific Rules

These rules apply only to WPF files (`.xaml` and `.xaml.cs`). They supplement the shared XAML rules, which also apply.

## Primary constructors are FORBIDDEN on code-behind

WPF code-behind classes **MUST NOT** use C# primary constructors. Code-behind must use an explicit constructor so the WPF designer can instantiate the class at design time:

```csharp
// ❌ WRONG — primary constructor
public partial class SettingsView(SettingsViewModel vm) : UserControl
{
    // ...
}

// ✅ CORRECT — explicit constructor with DI
public partial class SettingsView : UserControl
{
    public SettingsView(SettingsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
```

## Design-time ViewModel support

Use `d:DataContext` with `d:DesignInstance` to enable IntelliSense and design-time binding validation:

```xml
<UserControl x:Class="MyApp.Features.Settings.SettingsView"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             xmlns:vm="clr-namespace:MyApp.Features.Settings"
             mc:Ignorable="d"
             d:DataContext="{d:DesignInstance vm:SettingsViewModel, IsDesignTimeCreatable=False}">
```

Use `IsDesignTimeCreatable=False` when the ViewModel requires DI constructor parameters.

## Style and Trigger patterns

WPF uses `Style` with `TargetType` and `Trigger` / `DataTrigger` for visual state changes — not Avalonia's CSS-like selectors:

```xml
<Style x:Key="PrimaryButtonStyle" TargetType="Button"
       BasedOn="{StaticResource {x:Type Button}}">
  <Setter Property="Background" Value="{StaticResource TealBrush}" />
  <Setter Property="Foreground" Value="White" />
  <Style.Triggers>
    <Trigger Property="IsMouseOver" Value="True">
      <Setter Property="Background" Value="{StaticResource TealDkBrush}" />
    </Trigger>
    <Trigger Property="IsPressed" Value="True">
      <Setter Property="Background" Value="{StaticResource TealDkBrush}" />
    </Trigger>
    <Trigger Property="IsEnabled" Value="False">
      <Setter Property="Opacity" Value="0.7" />
    </Trigger>
  </Style.Triggers>
</Style>
```

Always include `IsMouseOver`, `IsPressed`, and `IsEnabled=False` triggers for interactive controls.

## Theme loading

Load theme resource dictionaries via `ResourceDictionary.MergedDictionaries`:

```xml
<Application.Resources>
  <ResourceDictionary>
    <ResourceDictionary.MergedDictionaries>
      <ResourceDictionary Source="/Themes/AppTheme.xaml" />
    </ResourceDictionary.MergedDictionaries>
  </ResourceDictionary>
</Application.Resources>
```

Use `pack://application:,,,/` URI syntax when referencing resources from other assemblies.

## `StartupUri` is FORBIDDEN

Never use `StartupUri` in `App.xaml`. It bypasses the DI container entirely — the framework creates the window directly. Instead, resolve the main window from DI in `App.xaml.cs`:

```xml
<!-- ❌ WRONG — bypasses DI -->
<Application StartupUri="MainWindow.xaml">

<!-- ✅ CORRECT — no StartupUri; resolve from DI in code-behind -->
<Application x:Class="MyApp.App">
```
