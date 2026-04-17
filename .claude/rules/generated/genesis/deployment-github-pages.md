---
# AUTO-GENERATED from .github/instructions/genesis/deployment-github-pages.instructions.md — do not edit
paths:
  - "**/deploy.yml"
---
# GitHub Pages Dual-Repo Deployment Setup

This guide covers the complete setup for deploying a static site to GitHub Pages using the
dual-repo pattern: a private repo for source code and a public repo for the published site.

## Step 1: Create the public deploy repo

Create a new **public** repository on GitHub. This repo will contain only the built output.

Naming convention: if your private repo is `my-org/website-private`, name the public one
`my-org/website-public` or `my-org/website`.

Initialize it with just a `README.md` — the deploy workflow will replace the contents.

## Step 2: Enable GitHub Pages on the public repo

In the public repo: Settings → Pages → Source → Deploy from a branch → Branch: `main`, folder: `/ (root)`.

## Step 3: Create a deploy token

Create a GitHub Personal Access Token (classic) or fine-grained PAT:

- **Classic PAT**: scope `repo` (full repo access)
- **Fine-grained PAT**: repository access for the **public deploy repo** only, permission
  Contents: Read and Write

## Step 4: Add the token as a secret

In the **private** (source) repo: Settings → Secrets and variables → Actions → New repository
secret → Name: `DEPLOY_REPO_TOKEN`, Value: the token from step 3.

## Step 5: Update the deploy workflow

In `.github/workflows/deploy.yml`, update the `DEPLOY_REPO` variable:

```yaml
DEPLOY_REPO: your-org/your-repo-public   # ← your public repo path
```

## Step 6: Custom domain

### Add CNAME to the public repo

Create a file named `CNAME` in the root of the public deploy repo containing your domain:

```
yourdomain.com
```

The deploy workflow preserves this file during deployments.

### Configure DNS

Add these DNS records at your domain registrar or DNS provider:

For apex domain (`yourdomain.com`):
```
A     @    185.199.108.153
A     @    185.199.109.153
A     @    185.199.110.153
A     @    185.199.111.153
```

For `www` subdomain:
```
CNAME www  your-org.github.io
```

### Enable HTTPS in GitHub Pages

In the public repo: Settings → Pages → Custom domain → enter your domain → check
"Enforce HTTPS". GitHub provisions a TLS certificate automatically (may take a few minutes).

### Update astro.config.mjs

Set the `site` property to your custom domain:

```javascript
site: 'https://yourdomain.com',
```

This affects sitemap generation, canonical URLs, and the robots.txt sitemap reference.

## Step 7: Cloudflare proxy and protection (optional)

If using Cloudflare as your DNS provider, you can proxy traffic through Cloudflare for
DDoS protection, caching, and analytics.

### Move DNS to Cloudflare

1. Create a Cloudflare account and add your domain
2. Update your domain registrar's nameservers to the ones Cloudflare provides
3. Wait for DNS propagation (can take up to 24 hours)

### Configure DNS records in Cloudflare

Add the same records as step 6, but with the **orange cloud (proxied)** enabled:

```
A     @    185.199.108.153    (Proxied)
A     @    185.199.109.153    (Proxied)
A     @    185.199.110.153    (Proxied)
A     @    185.199.111.153    (Proxied)
CNAME www  your-org.github.io (Proxied)
```

### SSL/TLS settings

In Cloudflare dashboard → SSL/TLS → set encryption mode to **Full**. Do NOT use "Full (strict)"
— GitHub Pages certificates are issued by Let's Encrypt, not Cloudflare's origin CA.

### Recommended Cloudflare settings

- **Caching → Tiered Cache**: enable for better cache hit rates
- **Speed → Auto Minify**: enable for HTML, CSS, JS
- **Security → Bot Fight Mode**: enable
- **Security → WAF**: review managed rules
- **Rules → Page Rules**: add `yourdomain.com/*` → Cache Level: Cache Everything (for static sites)

### Important: disable Cloudflare email obfuscation

Cloudflare injects JavaScript for email obfuscation that can break Astro's static HTML.
Disable it: Scrape Shield → Email Address Obfuscation → Off.
