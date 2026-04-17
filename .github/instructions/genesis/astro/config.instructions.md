---
applyTo: "**/astro.config.*"
---

# Astro Configuration Rules

## Required settings

Every Astro config must include:

```javascript
export default defineConfig({
  site: 'https://yourdomain.com',  // Required for sitemap, canonical URLs, OG tags
  output: 'static',                 // Static site generation
  integrations: [sitemap()],        // Always include sitemap
});
```

## Site URL

The `site` property must be set to the production domain. It is used by:
- `@astrojs/sitemap` for generating sitemap URLs
- `@astrojs/rss` for generating RSS feed URLs
- `Astro.url` for canonical URLs and OG tags
- `robots.txt` sitemap reference
- All JSON-LD structured data URLs

The template ships with `https://CHANGE-ME.example.com` — update this FIRST before creating
any pages. Never leave it as a placeholder in committed code. Every JSON-LD schema, every
canonical URL, and every sitemap entry depends on this value.

## RSS feed

Sites with blog content must include an RSS feed endpoint at `src/pages/feed.xml.ts`:

```typescript
import rss from '@astrojs/rss';
import { getCollection } from 'astro:content';

export async function GET(context) {
  const posts = await getCollection('blog', ({ data }) => !data.draft);
  return rss({
    title: 'Site Name',
    description: 'Site description',
    site: context.site,
    items: posts.map((post) => ({
      title: post.data.title,
      description: post.data.description,
      pubDate: post.data.date,
      link: `/blog/${post.id}/`,
    })),
  });
}
```

Link it in the layout `<head>`:
```html
<link rel="alternate" type="application/rss+xml" title="Site Name" href="/feed.xml" />
```

## Integrations

Declare all integrations in the config — not scattered across the codebase:
- `@astrojs/sitemap` — always included
- `@tailwindcss/vite` — via Vite plugin, not as an Astro integration

## Vite configuration

Tailwind CSS 4 uses the Vite plugin approach:

```javascript
import tailwindcss from '@tailwindcss/vite';

export default defineConfig({
  vite: {
    plugins: [tailwindcss()],
  },
});
```

## Environment variables

Access environment variables via `import.meta.env`:
- `PUBLIC_` prefix for client-exposed variables
- No prefix for server-only (build-time) variables

Never hardcode API keys, tokens, or secrets in the config. Use `.env` files (gitignored)
and reference via `import.meta.env.VARIABLE_NAME`.
