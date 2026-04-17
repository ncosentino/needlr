---
applyTo: "**/*.astro"
---

# SEO, AEO, and GEO Rules

These rules cover Search Engine Optimization (SEO), Answer Engine Optimization (AEO), and
Generative Engine Optimization (GEO) — ensuring content is discoverable by traditional search
engines, featured snippet systems, and LLM-based search tools.

## Search Engine Optimization (SEO)

### Robots meta — every page

```html
<meta name="robots" content="max-image-preview:large, max-snippet:-1, max-video-preview:-1" />
```

This maximizes how Google displays your content in search results. Use `noindex, follow` on
thin pages (tag listings, search results) to prevent index bloat while preserving link equity.

### Meta tags — every page

Every page must render in `<head>`:
- `<title>` — unique, under 60 characters, primary keyword near the front
- `<meta name="description">` — unique, 120-160 characters, compelling and keyword-rich
- `<link rel="canonical">` — self-referencing canonical URL
- `<meta name="author">` — site or article author name
- `<meta name="theme-color">` — brand color for browser chrome

### Open Graph — every page

```html
<meta property="og:title" content={title} />
<meta property="og:description" content={description} />
<meta property="og:type" content="website" />
<meta property="og:url" content={canonicalUrl} />
<meta property="og:image" content={ogImageUrl} />
<meta property="og:image:alt" content={ogImageAlt} />
<meta property="og:image:width" content="1200" />
<meta property="og:image:height" content="630" />
<meta property="og:image:type" content="image/png" />
<meta property="og:site_name" content="SiteName" />
<meta property="og:locale" content="en_US" />
```

For blog posts, use `og:type="article"` with:
```html
<meta property="article:published_time" content={publishDate} />
<meta property="article:modified_time" content={modifiedDate} />
<meta property="article:author" content={authorUrl} />
```

### Twitter cards

```html
<meta name="twitter:card" content="summary_large_image" />
<meta name="twitter:title" content={title} />
<meta name="twitter:description" content={description} />
<meta name="twitter:image" content={ogImageUrl} />
<meta name="twitter:image:alt" content={ogImageAlt} />
<meta name="twitter:site" content="@youraccount" />
<meta name="twitter:creator" content="@authoraccount" />
```

### RSS feed

Content-driven sites (blogs, news, podcasts) must include an RSS feed link in `<head>`:

```html
<link rel="alternate" type="application/rss+xml" title="Site Name" href="/feed.rss" />
```

### Heading hierarchy

- Exactly one `<h1>` per page — the primary topic
- Sequential heading levels: h1 → h2 → h3 — never skip levels
- Headings should be descriptive and include relevant keywords naturally

### Images

Use Astro's `<Image />` component or `<picture>` elements for optimized images:

```html
<picture>
  <source type="image/webp" srcset={webpUrl} />
  <img src={fallbackUrl} alt={descriptiveAlt} width="800" height="450"
       loading="lazy" decoding="async" />
</picture>
```

Rules:
- Every `<img>` has a descriptive `alt` attribute — never empty unless decorative (`alt=""` with `role="presentation"`)
- Include `width` and `height` to prevent layout shift (CLS)
- Use `loading="lazy"` and `decoding="async"` for below-the-fold images
- Use `fetchpriority="high"` for the hero/LCP image — only one per page
- Preload the LCP image in `<head>`:
  ```html
  <link rel="preload" as="image" href={heroImageUrl} />
  ```
- Use `srcset` for responsive images at multiple sizes
- Prefer WebP format with JPEG/PNG fallback via `<picture>`

### Internal linking

- Link to related pages within the site wherever contextually relevant
- Use descriptive anchor text — never "click here" or "read more"

### Index control

- Tag listing pages, search results pages, and filtered views: use `noindex, follow`
- Paginated pages: use `rel="next"` and `rel="prev"` where applicable
- Canonical URLs prevent duplicate content across pagination

### Performance

- Minimize JavaScript — Astro's static output is inherently fast
- Use `<link rel="preload">` for critical fonts and LCP images
- Keep Largest Contentful Paint (LCP) under 2.5 seconds
- Keep Cumulative Layout Shift (CLS) under 0.1

## Structured Data (Schema.org JSON-LD)

### Every page — WebSite + Organization

Include a base schema on every page:

```html
<script type="application/ld+json">
{
  "@context": "https://schema.org",
  "@type": "WebSite",
  "name": "SiteName",
  "url": "https://yourdomain.com",
  "potentialAction": {
    "@type": "SearchAction",
    "target": "https://yourdomain.com/search?q={search_term_string}",
    "query-input": "required name=search_term_string"
  }
}
</script>
```

The `SearchAction` enables the sitelinks search box in Google results. Include Organization
schema with `name`, `url`, `logo`, `description`, `contactPoint`, `sameAs` (social profiles).

### Blog posts — Article schema

```html
<script type="application/ld+json">
{
  "@context": "https://schema.org",
  "@type": ["BlogPosting", "LearningResource"],
  "headline": "Article Title",
  "description": "Article description",
  "datePublished": "2025-01-15",
  "dateModified": "2025-01-20",
  "author": {
    "@type": "Person",
    "name": "Author Name",
    "url": "https://yourdomain.com/about"
  },
  "publisher": {
    "@type": "Organization",
    "name": "SiteName",
    "logo": { "@type": "ImageObject", "url": "https://yourdomain.com/logo.png" }
  },
  "image": "https://yourdomain.com/images/article-hero.jpg",
  "speakable": {
    "@type": "SpeakableSpecification",
    "cssSelector": ["h1", ".article-summary"]
  }
}
</script>
```

The `SpeakableSpecification` tells voice assistants (Google Assistant, Alexa) which sections
to read aloud. Target the headline and opening summary.

### About / profile pages — Person schema

```html
<script type="application/ld+json">
{
  "@context": "https://schema.org",
  "@type": "ProfilePage",
  "mainEntity": {
    "@type": "Person",
    "name": "Name",
    "jobTitle": "Title",
    "worksFor": { "@type": "Organization", "name": "Company" },
    "sameAs": [
      "https://github.com/username",
      "https://linkedin.com/in/username",
      "https://twitter.com/username"
    ],
    "knowsAbout": ["topic1", "topic2"]
  }
}
</script>
```

### FAQ sections — FAQPage schema

Any page with Q&A content must include FAQPage schema:

```html
<script type="application/ld+json">
{
  "@context": "https://schema.org",
  "@type": "FAQPage",
  "mainEntity": [
    {
      "@type": "Question",
      "name": "What is this?",
      "acceptedAnswer": { "@type": "Answer", "text": "This is the answer." }
    }
  ]
}
</script>
```

### Breadcrumbs

Pages with hierarchical navigation must include BreadcrumbList schema:

```html
<script type="application/ld+json">
{
  "@context": "https://schema.org",
  "@type": "BreadcrumbList",
  "itemListElement": [
    { "@type": "ListItem", "position": 1, "name": "Home", "item": "https://yourdomain.com" },
    { "@type": "ListItem", "position": 2, "name": "Blog", "item": "https://yourdomain.com/blog" }
  ]
}
</script>
```

### Video content — VideoObject schema

Pages featuring video must include VideoObject schema:

```html
<script type="application/ld+json">
{
  "@context": "https://schema.org",
  "@type": "VideoObject",
  "name": "Video Title",
  "description": "Video description",
  "thumbnailUrl": "https://img.youtube.com/vi/VIDEO_ID/maxresdefault.jpg",
  "uploadDate": "2025-01-15",
  "contentUrl": "https://www.youtube.com/watch?v=VIDEO_ID"
}
</script>
```

Consider a separate `video-sitemap.xml` for sites with significant video content.

### Service/product pages

Use `Service`, `Product`, `LocalBusiness`, or `Organization` schema as appropriate for
the page content.

## Answer Engine Optimization (AEO)

AEO targets featured snippets, knowledge panels, and direct answers in search results.

### Question-based headings

Use question phrasing in headings where natural — search engines extract these for snippets:

```html
<h2>What does this service include?</h2>
<p>This service includes...</p>
```

### Concise answer paragraphs

Place a direct, concise answer (1-2 sentences) immediately after a question heading.
Follow with detailed explanation. Search engines prefer this "inverted pyramid" structure.

### Lists and tables

Use `<ul>`, `<ol>`, and `<table>` for structured information — these are preferred formats
for featured snippets. Never use images of text where real text would work.

### Definition patterns

For key terms, use clear definition patterns:

```html
<p><strong>Astro</strong> is a static site framework that delivers fast websites
by shipping zero JavaScript by default.</p>
```

## Generative Engine Optimization (GEO)

GEO ensures content is discoverable and correctly consumed by LLM-based search tools
(ChatGPT, Perplexity, Google AI Overviews, Bing Copilot).

### llms.txt

Include a `/llms.txt` file in the public directory and link to it in `<head>`:

```html
<link rel="alternate" type="text/plain" href="/llms.txt" />
```

Content should describe the site, list key pages with descriptions, and provide contact info:

```
# SiteName

> Brief description of what this site/organization does.

## Pages

- [Home](https://yourdomain.com): Main page description
- [About](https://yourdomain.com/about): About page description
- [Services](https://yourdomain.com/services): Services overview

## Contact

- Email: contact@yourdomain.com
```

### Structured data completeness

LLMs consume JSON-LD structured data. The more complete and accurate your schema markup,
the better LLMs can cite and summarize your content. Always include:
- Organization schema with `name`, `url`, `logo`, `description`, `contactPoint`, `sameAs`
- Article schema with `author`, `datePublished`, `dateModified`, `speakable`
- Service/Product schema with `name`, `description`, `provider`

### Content clarity

- Write in clear, factual prose — LLMs prefer unambiguous statements over marketing fluff
- Include specific numbers, dates, and facts that can be cited
- Avoid vague claims like "industry-leading" — use specific evidence

### robots.txt — allow AI crawlers

```
User-agent: *
Allow: /

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

Sitemap: https://yourdomain.com/sitemap-index.xml
```

### Sitemap

Auto-generated via `@astrojs/sitemap` and referenced in `robots.txt`. Consider a separate
video sitemap for sites with significant video content.

### humans.txt

Include a `/humans.txt` in the public directory crediting the team and technologies:

```
/* TEAM */
Name: Your Name
Role: Role
Contact: email@domain.com

/* SITE */
Last update: YYYY-MM-DD
Language: English
Standards: HTML5, CSS3
Software: Astro, Tailwind CSS
```