---
applyTo: "**/*.axaml,**/*.axaml.cs"
---

# Central Color Theming Rules

Colors live in exactly one place: the theme dictionary under a `Themes/` folder (this template
ships `Themes/AppTheme.axaml` in the App project). That file is the single source of truth. Views
and code-behind **reference** its tokens; they never define a color of their own. Read the theme
file to discover the available tokens — do not restate them here or anywhere else.

## In views (`*.axaml`)

Never hardcode a color — no raw `#RGB`/`#RRGGBB`/`#AARRGGBB` literal, no inline `<SolidColorBrush>`
or `<Color>`, and no named framework color (`"White"`, `"Black"`, or `Colors.*`) as a paint value.
Bind every brush to a theme token instead:

```xml
<!-- ❌ WRONG — hardcoded hex, inline brush, literal "White" -->
<Border Background="#FEF3C7" BorderBrush="White">
  <Border.Effect><SolidColorBrush Color="#B91C1C" /></Border.Effect>
</Border>

<!-- ✅ CORRECT — references brush tokens from the theme -->
<Border Background="{StaticResource SurfaceBrush}" BorderBrush="{StaticResource BorderBrush}" />
```

To choose the right token, open the theme file and pick the brush whose **role** matches your
intent — prefer a token named for its semantic role over one named for a raw palette color. If
nothing there fits, **add a new semantic token to the theme first, then reference it**. Never
inline a one-off value "just this once."

## In code-behind (`*.axaml.cs`)

The same rule applies: no `Color.Parse("#...")`, `Colors.*`, or `new SolidColorBrush(<literal>)`.
Express state-driven color as a `ControlTheme` in the theme (pseudo-class selectors) and bind to
it, rather than assigning colors from code.

## Editing the theme dictionary (`Themes/*.axaml`)

The theme dictionary is the one place a raw color literal belongs. Define a `Color` primitive
paired with a `SolidColorBrush` (views reference the brush), name it for its semantic role, and
keep the palette grouped by role. This is where new colors are born — everywhere else only
references them.
