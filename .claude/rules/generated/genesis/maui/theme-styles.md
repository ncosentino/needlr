---
# AUTO-GENERATED from .github/instructions/genesis/maui/theme-styles.instructions.md — do not edit
paths:
  - "**/*.xaml"
  - "**/*.xaml.cs"
---
# Central Style Theming Rules

Colors and typography live in exactly one place: `Resources/Styles/Colors.xaml` (color and brush
resources) and `Resources/Styles/Styles.xaml` (named `Style` resources built from them). Both are
merged into `App.xaml` automatically. Views **reference** a `Style` or a `{StaticResource}` brush;
they never set appearance properties from a literal.

## In views (`*.xaml`)

Never set `TextColor`, `BackgroundColor`, `FontSize`, or `FontFamily` to a literal on a control
instance. Apply a `Style` instead — either an implicit `TargetType` style that already covers the
control, or an explicit named style:

```xml
<!-- ❌ WRONG — literal font size and color set directly on the control -->
<Label Text="{Binding CountText}" FontSize="18" TextColor="#1E293B" />

<!-- ✅ CORRECT — references a named Style built from theme resources -->
<Label Text="{Binding CountText}" Style="{StaticResource Strong}" />
```

If no existing style fits, **add one to `Styles.xaml` first** (built from `Colors.xaml` brushes),
then reference it. Never invent a one-off literal in a view because "it's just this one label."

## In code-behind (`*.xaml.cs`)

The same rule applies: no `TextColor = Color.FromArgb(...)`, no `FontSize = <number>` assigned in
code. If a control's appearance needs to change at runtime, switch it to a different named style
or a `VisualState`, rather than assigning literal property values.

## Editing `Colors.xaml` / `Styles.xaml`

`Colors.xaml` is the one place a raw color literal belongs — define a `Color` paired with a
`SolidColorBrush`, named for its semantic role. `Styles.xaml` is where reusable appearance
combinations (font size, weight, color) are named and built from those brushes. This is where new
tokens and styles are born — everywhere else only references them.
