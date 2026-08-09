---
applyTo: "**/*.astro"
---

# Astro discovery metadata

Use the template's shared layout/site configuration instead of duplicating head
metadata per page.

## Page head

Every indexable page provides:

- one unique, descriptive `<title>`;
- one accurate meta description;
- a canonical URL;
- the intended robots policy;
- Open Graph title, description, type, URL, image, and image alt;
- Twitter card metadata matching the same content.

Content pages include accurate published/modified/author values. Do not fabricate
dates, people, claims, social accounts, or image dimensions.

Content-driven sites expose their existing RSS feed in the head. Do not add an empty
feed to a site without published content.

## Document semantics

- One visible `<h1>` names the page.
- Heading levels stay sequential.
- Links use descriptive text rather than generic calls to click/read.
- Images use descriptive alt text, intrinsic dimensions, responsive sources, and
  lazy loading below the fold.
- Only the actual LCP image is high priority/preloaded.

Long headings, URLs, and project names must wrap without horizontal overflow.

## Structured data

JSON-LD reflects visible page content and uses canonical absolute URLs.

- Site/root pages may use `WebSite` and `Organization`.
- Articles use `Article`/`BlogPosting` with real author/date/image data.
- Hierarchical pages use `BreadcrumbList`.
- FAQ, video, service, product, person, or local-business schema is emitted only when
  the page visibly contains that content.

Validate generated JSON and keep structured data synchronized with rendered content.

## Crawlers and answer surfaces

- Keep `robots.txt`, sitemap, canonical URLs, and index policy consistent.
- `llms.txt`/`humans.txt` are optional project artifacts; when present they contain
  current factual links and contact/project information.
- Use concise answers, semantic lists/tables, and clear definitions when they improve
  the reader's page—not as hidden search-engine text.
- Do not claim crawler behavior, rankings, snippets, or AI citation outcomes that the
  generated site cannot verify.

## Performance

Use Astro image/static-output primitives and the repository's declared performance
gate. Avoid duplicate preload, unnecessary client hydration, and layout-shifting media.
