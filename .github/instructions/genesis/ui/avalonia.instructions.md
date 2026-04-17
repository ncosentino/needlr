---
applyTo: "**/*.axaml,**/*.axaml.cs"
---

# Avalonia-Specific Rules

These rules apply only to Avalonia files (`.axaml` and `.axaml.cs`). They supplement the shared XAML rules, which also apply.

## Design-time constructor generation

Every Avalonia code-behind class with a parameterized constructor **MUST** be decorated with `[GenerateAvaloniaDesignTimeConstructor]` from Needlr. This generates a parameterless constructor that the Avalonia designer requires:

```csharp
using NexusLabs.Needlr.Avalonia;

[GenerateAvaloniaDesignTimeConstructor]
public partial class ChooseView : UserControl
{
    public ChooseView(ChooseViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
```

Without this attribute, the Avalonia previewer will fail to render the view at design time.

## Primary constructors are FORBIDDEN on code-behind

Avalonia code-behind classes **MUST NOT** use C# primary constructors. The Needlr source generator cannot generate a design-time constructor for primary-constructor classes because primary constructor parameters become implicit fields with no parameterless alternative.

```csharp
// ❌ WRONG — primary constructor breaks design-time generation
[GenerateAvaloniaDesignTimeConstructor]
public partial class ChooseView(ChooseViewModel vm) : UserControl
{
    // ...
}

// ✅ CORRECT — explicit constructor
[GenerateAvaloniaDesignTimeConstructor]
public partial class ChooseView : UserControl
{
    public ChooseView(ChooseViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
```

## ControlTheme for custom control appearance

Custom control styles in Avalonia use `ControlTheme` with `BasedOn` to extend the default theme, not standalone `Style` blocks:

```xml
<ControlTheme x:Key="PrimaryButton" TargetType="Button"
              BasedOn="{StaticResource {x:Type Button}}">
  <Setter Property="Background" Value="{StaticResource TealBrush}" />
  <Setter Property="Foreground" Value="White" />
</ControlTheme>
```

## Visual state styling

Avalonia uses CSS-like pseudo-class selectors for visual states. Always define states within the `ControlTheme`, not as separate top-level styles:

```xml
<ControlTheme x:Key="PrimaryButton" TargetType="Button"
              BasedOn="{StaticResource {x:Type Button}}">
  <Setter Property="Background" Value="{StaticResource TealBrush}" />

  <Style Selector="^:pointerover /template/ ContentPresenter#PART_ContentPresenter">
    <Setter Property="Background" Value="{StaticResource TealDkBrush}" />
  </Style>
  <Style Selector="^:pressed /template/ ContentPresenter#PART_ContentPresenter">
    <Setter Property="Background" Value="{StaticResource TealDkBrush}" />
  </Style>
  <Style Selector="^:disabled /template/ ContentPresenter#PART_ContentPresenter">
    <Setter Property="Opacity" Value="0.7" />
  </Style>
</ControlTheme>
```

The `^` selector refers to the parent `ControlTheme`'s target type. Always include `:pointerover`, `:pressed`, and `:disabled` states for interactive controls.
