---
# AUTO-GENERATED from .github/instructions/genesis/avalonia/theme-fonts.instructions.md — do not edit
paths:
  - "**/*.axaml"
  - "**/*.axaml.cs"
---
# Central Font & Font-Size Theming Rules

Text sizes live in exactly one place: the size scale in the theme dictionary under a `Themes/`
folder (this template ships `Themes/AppTheme.axaml` in the App project). That file is the single
source of truth. Views **reference** the size tokens and never set a font of their own. Read the
theme file to discover the available size tokens — do not restate them here.

## In views (`*.axaml`)

Never hardcode a font — no inline numeric `FontSize`, and no inline `FontFamily`:

```xml
<!-- ❌ WRONG — hardcoded size -->
<TextBlock FontSize="14" Text="{Binding Heading}" />

<!-- ✅ CORRECT — size from the theme scale -->
<TextBlock FontSize="{StaticResource FontSizeMD}" Text="{Binding Heading}" />
```

To choose a size, open the theme file and pick the token whose step matches your intent. If
nothing there fits, **add a token to the theme first** (or snap to the nearest existing one) —
never invent a one-off number in a view. If the app registers a custom typeface at startup, set
`FontFamily` once at that level rather than per-view.

## In code-behind (`*.axaml.cs`)

The same rule applies: no `FontSize = <number>` and no `new FontFamily("...")`. Typography is a
theming concern — keep it in XAML tokens or a `ControlTheme`.

## Editing the theme dictionary (`Themes/*.axaml`)

The theme dictionary is where the font-size scale is defined; keep it canonical and role-named
(one token per intended size).
