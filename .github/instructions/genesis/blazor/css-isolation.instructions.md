---
applyTo: "**/*.razor.css"
---

# Blazor CSS Isolation Rules

These rules apply to component-scoped CSS files (`.razor.css`).

## CSS isolation overview

Blazor supports CSS isolation by convention: a file named `Component.razor.css` is scoped
automatically to `Component.razor`. The framework rewrites selectors at build time to include
a unique scope identifier.

## File naming

The CSS file must exactly match the component file name with `.css` appended:

```
ProductDetail.razor
ProductDetail.razor.css    ← scoped to ProductDetail only
```

## Rules

- Use component-scoped CSS for component-specific styles
- Do not use `!important` — scoped CSS already has high specificity via the scope attribute
- Avoid deep combinator `::deep` unless styling child components that cannot be modified
- Keep styles minimal — prefer CSS classes over inline styles in markup
- Use CSS custom properties (variables) for theming rather than hardcoded values:

```css
.product-card {
    background-color: var(--surface-color, #ffffff);
    border-radius: var(--border-radius, 4px);
    padding: 1rem;
}
```

## Shared styles

For styles shared across multiple components, use a global stylesheet in `wwwroot/css/` or a
shared Razor class library. Do not duplicate CSS across component-scoped files.
