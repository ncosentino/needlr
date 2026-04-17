---
applyTo: "**/*.astro"
---

# Astro Component Rules

## Naming

- PascalCase filenames: `FeatureCard.astro`, `HeroSection.astro`
- Organize by purpose: `components/layout/`, `components/ui/`, `components/sections/`
- One component per file

## Props

Every component that accepts data must define a `Props` interface in the frontmatter:

```astro
---
interface Props {
  title: string;
  description?: string;
}

const { title, description = 'Default description' } = Astro.props;
---
```

Do not use `any` or untyped props. Destructure with defaults where appropriate.

## Composition

- Use `<slot />` for content projection in wrapper/shell components
- Named slots (`<slot name="header" />`) for multi-region layouts
- Prefer composition over prop drilling — pass content via slots, not large prop objects

## Markup

- Use semantic HTML elements: `<header>`, `<nav>`, `<main>`, `<section>`, `<article>`, `<footer>`, `<aside>`
- Every `<img>` must have a meaningful `alt` attribute — never empty unless purely decorative (then use `alt=""` and `role="presentation"`)
- Every interactive element must be keyboard accessible — use `<button>` for actions, `<a>` for navigation
- Use Astro's built-in `<Image />` component for optimized images instead of raw `<img>` tags

## Styling

- Use Tailwind CSS utility classes — no inline `style=` attributes
- Component-scoped styles via `<style>` blocks are acceptable for complex animations or states that Tailwind doesn't cover
- Never use `!important`

## Frontmatter

Keep frontmatter logic minimal:
- Import components and data
- Destructure props
- Simple computed values

Move complex logic to utility functions in `.ts` files. The frontmatter is not the place for business logic.

## Mobile navigation

Mobile menu toggles must be fully accessible:

- Toggle button needs `aria-expanded` (true/false) and `aria-controls` pointing to the menu ID
- Menu container needs a unique `id` matching `aria-controls`
- Pressing Escape must close the menu
- Focus must be trapped inside the open menu
- When closing, focus returns to the toggle button

```html
<button aria-expanded="false" aria-controls="mobile-menu" aria-label="Open menu">
  <!-- hamburger icon -->
</button>
<nav id="mobile-menu" hidden>
  <!-- nav links -->
</nav>
```

## Interactivity

Astro components render to static HTML by default. For interactive behavior:
- Use `<script>` tags for vanilla JS (preferred for simple interactions)
- Use framework islands (React, Svelte, Vue) only when complex client-side state is required
- Always add `client:load`, `client:visible`, or `client:idle` directives — never `client:only` unless SSR is impossible
- Keep islands small and focused — do not wrap entire pages in a framework component
