---
# AUTO-GENERATED from .github/instructions/genesis/astro/pages.instructions.md — do not edit
paths:
  - "**/pages/**/*.astro"
---
# Astro Page Rules

## Layout requirement

Every page must use a layout. No page should render a bare `<html>` document directly:

```astro
---
import BaseLayout from '../layouts/BaseLayout.astro';
---

<BaseLayout title="Page Title" description="Page description for SEO">
  <!-- page content -->
</BaseLayout>
```

## Required meta

Every page must provide:
- `title` — unique, descriptive, under 60 characters
- `description` — unique per page, 120-160 characters, includes primary keyword

These are passed as props to the layout, which renders them in `<head>`.

## File-based routing

- `src/pages/index.astro` → `/`
- `src/pages/about.astro` → `/about`
- `src/pages/blog/index.astro` → `/blog`
- `src/pages/blog/[slug].astro` → `/blog/:slug` (dynamic route)

Use folders for route groups. Use `[param].astro` for dynamic routes with `getStaticPaths()`.

## Dynamic routes

Dynamic routes must export `getStaticPaths()`:

```astro
---
export async function getStaticPaths() {
  const posts = await getCollection('blog', ({ data }) => !data.draft);
  return posts.map((post) => ({
    params: { slug: post.slug },
    props: { post },
  }));
}

const { post } = Astro.props;
---
```

## Error pages

- `404.astro` must exist at `src/pages/404.astro`
- It must use the same layout as other pages for visual consistency
- Include a clear message and a link back to the homepage
- 404 pages must use `<meta name="robots" content="noindex">` — they should never be indexed

## Pages that must NOT be indexed

Add `<meta name="robots" content="noindex, follow">` to:
- 404 error pages
- Search results pages
- Tag/category listing pages with thin content
- Any utility or internal pages

Pass a `noindex` prop to the layout to control this per page.

## Home page structured data

The home page must include `WebSite` schema with `SearchAction` (if the site has search)
and `Organization` or business-type schema. This is in ADDITION to whatever the layout provides.

## Dynamic routes must filter drafts

`getStaticPaths()` must ALWAYS filter out draft content. Never generate pages for draft items:

```astro
const posts = await getCollection('blog', ({ data }) => !data.draft);
```

This applies to blog posts, services, and any content collection with a `draft` field.

## Page content

- Use `<h1>` exactly once per page — it is the primary heading for SEO
- Heading hierarchy must be sequential: `<h1>` → `<h2>` → `<h3>` — never skip levels
- Include internal links to other pages where contextually relevant
