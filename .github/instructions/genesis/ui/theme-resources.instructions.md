---
applyTo: "**/Themes/**/*xaml"
---

# Theme Resource Dictionary Rules

These rules apply to theme and resource dictionary files within `Themes/` folders. The general XAML view/control rules do not apply here — theme files are resource definitions, not views.

## Location

Theme resource dictionaries live in a `Themes/` folder within the project or feature that owns them:

```
Features/Shell/
  Themes/
    AppTheme.axaml          (Avalonia)
    AppTheme.xaml            (WPF)
```

A project may have one shared theme or multiple theme files split by concern (e.g., `Colors.axaml`, `Typography.axaml`, `ButtonStyles.axaml`). When splitting, keep a single entry-point file that merges the others.

## Structure — layered definitions

Define resources in this order within a theme file:

1. **Colors** — raw `Color` resources
2. **Brushes** — `SolidColorBrush` references to the colors above
3. **Design tokens** — `CornerRadius`, font sizes (`sys:Double`), `Thickness` for spacing
4. **Control themes / styles** — `ControlTheme` (Avalonia) or `Style` (WPF) for custom control appearances

Each layer references only the layers above it. Never reference a `ControlTheme`/`Style` from a `Color` definition.

## Resource key naming

Use `PascalCase` with a suffix that indicates the resource type:

| Type | Suffix | Example |
|------|--------|---------|
| `Color` | `*Color` | `TealColor`, `SlateColor` |
| `SolidColorBrush` | `*Brush` | `TealBrush`, `BorderBrush` |
| `CornerRadius` | `Radius*` | `RadiusSmall`, `RadiusLarge` |
| `sys:Double` (font) | `FontSize*` | `FontSizeSM`, `FontSizeXL` |
| `Thickness` | descriptive | `ContentPadding`, `FooterPadding` |
| `ControlTheme` / `Style` | descriptive | `PrimaryButton`, `SecondaryButton` |

## Colors and Brushes — always paired

Every color used for UI painting must have both a `Color` and a `SolidColorBrush` resource. Controls reference the `*Brush` resource, not the `*Color` directly:

```xml
<Color x:Key="TealColor">#1FA79B</Color>
<SolidColorBrush x:Key="TealBrush" Color="{StaticResource TealColor}" />
```

This separation allows brush-level overrides (e.g., opacity, gradient substitution) without changing the color palette.

## No inline styling on controls

All custom control appearances are defined as named `ControlTheme` (Avalonia) or `Style` (WPF) entries in theme files. Controls in view XAML reference these by key — they never set visual properties directly:

```xml
<!-- ✅ CORRECT — references a themed control style -->
<Button Theme="{StaticResource PrimaryButton}" Content="{Binding SubmitText}" />

<!-- ❌ WRONG — inline styling bypasses the theme -->
<Button Background="#1FA79B" Foreground="White" CornerRadius="10"
        Content="{Binding SubmitText}" />
```

## Interactive control states

Every `ControlTheme` / `Style` for an interactive control must define visual states for at least:

- Default (normal state)
- Hover / pointer-over
- Pressed
- Disabled

Omitting states causes jarring visual transitions when the base theme's defaults leak through.
