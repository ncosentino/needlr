---
# AUTO-GENERATED from .github/instructions/genesis/astro/content-collections.instructions.md — do not edit
paths:
  - "**/content/content.config.ts"
  - "**/content/**/*.md"
  - "**/content/**/*.mdx"
---
# Astro Content Collections Rules

## Schema definition

Every content collection must have a Zod schema defined in `src/content/content.config.ts`:

```typescript
import { defineCollection, z } from 'astro:content';

const blog = defineCollection({
  schema: z.object({
    title: z.string(),
    description: z.string(),
    date: z.date(),
    draft: z.boolean().default(false),
    tags: z.array(z.string()).default([]),
  }),
});

export const collections = { blog };
```

Do not use untyped content. Every field accessed in templates must be declared in the schema.

## Folder structure

One folder per collection under `src/content/`:

```
src/content/
  blog/
    my-first-post.md
    astro-best-practices.mdx
  services/
    web-development.md
    consulting.md
  content.config.ts
```

## Frontmatter

Every markdown/MDX file must include frontmatter matching its collection schema:

```markdown
---
title: "My First Post"
description: "A brief introduction to the blog."
date: 2025-01-15
tags: ["astro", "web"]
---
```

## Querying

Use `getCollection()` in pages, never raw file imports:

```astro
---
import { getCollection } from 'astro:content';

const posts = await getCollection('blog', ({ data }) => !data.draft);
---
```

Filter drafts in production. Sort by date descending for blog-style content.

## MDX

Use MDX (`.mdx`) when content needs interactive components. Use plain markdown (`.md`) for
pure text content. Do not use MDX everywhere — it adds build overhead.

## SEO for content

Every content item should have:
- A unique `title` (used for `<title>` and `<h1>`)
- A unique `description` (used for meta description)
- Structured data where appropriate (Article schema for blog posts)
