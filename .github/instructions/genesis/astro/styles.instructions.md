---
applyTo: "**/styles/**/*.css"
---

# Astro Stylesheet Rules

## Tailwind CSS as primary

Tailwind CSS is the primary styling approach. The global CSS file should import Tailwind:

```css
@import 'tailwindcss';
```

Additional global styles (custom properties, font imports, base resets) are placed here.

## No component CSS files

Do not create separate `.css` files per component. Use either:
- Tailwind utility classes directly in markup (preferred)
- Scoped `<style>` blocks inside `.astro` components for complex cases

## Custom properties

Define design tokens as CSS custom properties in the global stylesheet for values that
Tailwind doesn't cover or that need to be referenced from JavaScript:

```css
@import 'tailwindcss';

:root {
  --color-brand: #1a1a2e;
  --font-display: 'Inter Variable', system-ui, sans-serif;
}
```

## No inline styles

Never use `style=` attributes in markup. Use Tailwind classes or CSS custom properties.

## Dark mode

If supporting dark mode, use Tailwind's `dark:` variant with the `class` strategy.
Define dark mode colors alongside light mode in the global stylesheet or Tailwind config.

## Print styles

For content-heavy pages (articles, documentation), include basic print styles:

```css
@media print {
  header, footer, nav { display: none; }
  main { max-width: 100%; }
}
```
