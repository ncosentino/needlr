---
applyTo: "**/layouts/**/*.astro"
---

# Astro Layout Rules

## Purpose

Layouts define the outer shell of a page — `<html>`, `<head>`, `<body>`, and shared elements like
header, footer, and navigation. All pages use a layout via composition.

## Required structure

Every layout must include:

```astro
<!doctype html>
<html lang="en">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <meta name="description" content={description} />
    <link rel="icon" type="image/svg+xml" href="/favicon.svg" />
    <title>{title}</title>
    <!-- Open Graph and Twitter meta tags -->
  </head>
  <body>
    <slot />
  </body>
</html>
```

## Props

Layouts must accept at minimum:
- `title` — page title for `<title>` and OG tags
- `description` — page description for meta and OG tags

```astro
---
interface Props {
  title: string;
  description?: string;
  ogImage?: string;
}
---
```

## Open Graph and social meta

Every layout must render Open Graph tags for social sharing:

```html
<meta property="og:title" content={title} />
<meta property="og:description" content={description} />
<meta property="og:type" content="website" />
<meta property="og:url" content={Astro.url.href} />
<meta property="og:image" content={ogImage || '/og-default.png'} />
<meta name="twitter:card" content="summary_large_image" />
```

## Canonical URL

Include a canonical URL to prevent duplicate content issues:

```html
<link rel="canonical" href={Astro.url.href} />
```

## Multiple layouts

Use separate layouts for distinct page types:
- `BaseLayout.astro` — default for most pages
- `BlogLayout.astro` — blog posts with article schema, author, date
- `DocsLayout.astro` — documentation with sidebar navigation

Each specialized layout can wrap `BaseLayout` to avoid duplicating head/meta logic.
