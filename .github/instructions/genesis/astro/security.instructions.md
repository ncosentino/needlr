---
applyTo: "**/*.astro,**/astro.config.*,**/.env*"
---

# Security Rules for Astro Sites

## Secrets and environment variables

### Never commit secrets

API keys, tokens, passwords, and credentials must NEVER appear in source code, config files,
or markup. Use environment variables exclusively.

### .env files are gitignored

The `.gitignore` must include:
```
.env
.env.*
!.env.example
```

Provide a `.env.example` file with placeholder values so developers know which variables
are needed without exposing real values.

### Public vs private variables

Astro exposes variables with the `PUBLIC_` prefix to client-side code. This means they are
visible in the browser — treat them accordingly:

- `PUBLIC_SITE_URL` — safe (not a secret)
- `PUBLIC_ANALYTICS_ID` — safe (analytics IDs are inherently public)
- `API_SECRET_KEY` — never `PUBLIC_` prefix (server/build-time only)

```javascript
// ✅ Safe — build-time only
const apiKey = import.meta.env.API_SECRET_KEY;

// ❌ DANGEROUS — exposed to browser
const apiKey = import.meta.env.PUBLIC_API_SECRET_KEY;
```

## Content Security Policy

For sites that include any JavaScript, consider adding CSP headers via Cloudflare page rules
or a `_headers` file (for platforms that support it):

```
/*
  Content-Security-Policy: default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'
```

For static Astro sites with no dynamic scripts, CSP is less critical but still recommended.

## Forms

If the site includes contact forms or any user input:
- Never process form data client-side with secrets
- Use a form service (Formspree, Netlify Forms) or a serverless function
- Always validate and sanitize input server-side
- Include honeypot fields or CAPTCHA for spam protection
- Never echo user input back into HTML without escaping (XSS prevention)

## Third-party scripts

- Load analytics and tracking scripts with `async` or `defer`
- Prefer privacy-respecting analytics (Plausible, Fathom) over Google Analytics where possible
- Audit third-party scripts for security — each one is an attack vector
- Use Subresource Integrity (SRI) hashes for CDN-hosted scripts

## Deploy secret security

The Cloudflare Pages deploy workflow authenticates with two GitHub repository secrets:

- `CLOUDFLARE_API_TOKEN` — scope it to **Account › Cloudflare Pages › Edit** only, so it cannot
  reach DNS, other zones, or any non-Pages resource.
- `CLOUDFLARE_ACCOUNT_ID` — the target Cloudflare account id.

Both must:
- Never be logged or echoed in workflow output
- Be rotated periodically
- Never be stored in `.env` files or source code
