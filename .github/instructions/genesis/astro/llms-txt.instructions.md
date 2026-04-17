---
applyTo: "**/llms.txt"
---

# llms.txt Rules

## Purpose

`llms.txt` is the AI-readable equivalent of `robots.txt`. It tells LLM-based search tools
(ChatGPT, Perplexity, Google AI Overviews) what the site is about and how to navigate it.
It lives in the `public/` directory and is served at the site root.

## Discovery

The file must be linked in the site's `<head>` so LLMs can find it programmatically:

```html
<link rel="alternate" type="text/plain" href="/llms.txt" />
```

This goes in the layout component's `<head>` section.

## Required structure

```
# SiteName

> One-line description of the site/organization and what it does.

## Pages

- [Home](https://yourdomain.com): What the homepage covers
- [About](https://yourdomain.com/about): Who runs this, background, mission
- [Services](https://yourdomain.com/services): What services are offered

## Contact

- Email: contact@yourdomain.com
- Location: City, State (if applicable)
- Phone: (if applicable)
```

## Content guidelines

- Be factual and specific — LLMs will quote this directly
- Use the full canonical URL for every link
- Describe each page in one clear sentence
- Include the most important pages — not every page
- Update when pages are added or removed
- Do not include marketing language or superlatives
- Include contact information if publicly available

## For content-heavy sites

Add sections for content categories:

```
## Blog

- [Latest posts](https://yourdomain.com/blog): Technology articles and tutorials
- [Category: .NET](https://yourdomain.com/blog/category/dotnet): C# and .NET content

## Videos

- [All videos](https://yourdomain.com/videos): Video content library
```

## Do NOT include

- Secrets, API keys, or internal URLs
- Passwords or authentication endpoints
- Private/draft content URLs
- Personally identifiable information beyond what's publicly available on the site
