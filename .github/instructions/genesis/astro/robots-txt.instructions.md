---
applyTo: "**/robots.txt"
---

# robots.txt Rules

## Purpose

`robots.txt` controls which crawlers can access which parts of the site. It lives in the
`public/` directory and is served at the site root.

## Required entries

Every `robots.txt` must include:

```
User-agent: *
Allow: /

Sitemap: https://yourdomain.com/sitemap-index.xml
```

## AI crawler access

Explicitly allow AI crawlers unless the site owner has decided to block them:

```
User-agent: GPTBot
Allow: /

User-agent: ChatGPT-User
Allow: /

User-agent: anthropic-ai
Allow: /

User-agent: ClaudeBot
Allow: /

User-agent: PerplexityBot
Allow: /

User-agent: Googlebot
Allow: /
```

If the site owner wants to block AI training while allowing search, use `Disallow` selectively
for training-specific bots but keep `Googlebot` and search bots allowed.

## What to disallow

- API endpoints (if any): `Disallow: /api/`
- Admin/internal routes: `Disallow: /admin/`
- Search results pages: `Disallow: /search`

Do NOT disallow CSS, JS, or image paths — search engines need these to render pages properly.

## Sitemap references

List every sitemap the site produces:

```
Sitemap: https://yourdomain.com/sitemap-index.xml
Sitemap: https://yourdomain.com/video-sitemap.xml
```

Use the full absolute URL, not relative paths.

## Domain must match

The sitemap URL domain must match the `site` value in `astro.config.mjs`. Mismatches cause
search engines to ignore the sitemap.
