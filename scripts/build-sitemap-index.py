#!/usr/bin/env python3
"""
Build a sitemap index + per-slice sitemaps for the deployed site.

Usage:
    python scripts/build-sitemap-index.py <site-dir> [--current-release <version>]

Rationale:
    mkdocs writes a single monolithic sitemap.xml containing every page it
    knows about at build time. With our split-deploy model (ci.yml only
    sees /api/dev/ + main pages, release.yml only sees the slice being
    released), neither build has full knowledge of all live content on
    gh-pages. The last-deploy-wins sitemap therefore systematically
    underrepresents the API reference — e.g. 0 of ~3000 historical v*/
    pages are indexed because ci.yml runs far more often than release.yml.

    This script replaces mkdocs's default sitemap with a multi-level
    layout compliant with the sitemaps.org protocol:

        /sitemap.xml                         (sitemap index)
        /sitemap-main.xml                    (home, features, articles...)
        /api/dev/sitemap.xml                 (dev API reference slice)
        /api/stable/sitemap.xml              (stable API reference slice)
        /api/v<version>/sitemap.xml          (each preserved version)

    The index references every sub-sitemap:
      - local ones just written from the current build's ./site/
      - remote ones that exist on gh-pages (discovered via the GitHub
        contents API, matching the logic in versions.json generation)
      - the version being released right now (passed as --current-release),
        whose sub-sitemap is created by THIS invocation inside ./site/
        even though it doesn't yet exist on gh-pages

    Both ci.yml and release.yml invoke this. Neither touches the other's
    sub-sitemaps because of peaceiris keep_files:true + existing rm -rf
    steps that enforce slice ownership. Both can freely overwrite
    /sitemap.xml because the index content is deterministic from
    gh-pages + current-release state.
"""
from __future__ import annotations

import argparse
import json
import os
import sys
import urllib.request
from datetime import datetime, timezone
from pathlib import Path
from xml.sax.saxutils import escape

SITE_URL = 'https://github.devleader.ca/needlr'
GH_PAGES_API = 'https://api.github.com/repos/ncosentino/needlr/contents/api?ref=gh-pages'

# Files in ./site/ that should NOT appear in any sitemap
EXCLUDE_HTML_NAMES = frozenset({'404.html'})

# URL path prefixes (relative to SITE_URL) that should NOT appear in any
# sitemap. Everything matching one of these is dropped before slice
# categorization.
#
# - coverage/*       — internal code coverage reports (hundreds of .html
#                      files per deploy), noise for crawlers
# - overrides/*      — mkdocs template files accidentally copied to site
#                      output because docs/overrides/ is inside docs_dir;
#                      contains Jinja source that shouldn't be public
# - dev/*            — legacy mike deploy content from an abandoned
#                      versioning experiment, persisted on gh-pages but
#                      not referenced by the current site. NOTE: this is
#                      the /dev/ at the site root, NOT /api/dev/ which is
#                      our legitimate development API reference slice.
EXCLUDE_URL_PREFIXES = (
    'coverage/',
    'overrides/',
    'dev/',
)


def parse_args():
    p = argparse.ArgumentParser()
    p.add_argument('site_dir', type=Path, help='Path to the mkdocs ./site/ directory')
    p.add_argument('--current-release', default=None,
                   help='Version tag for release.yml invocations (e.g. "0.0.2-alpha.27"); '
                        'ensures the sub-sitemap we are about to deploy is referenced in the index')
    return p.parse_args()


def walk_html_urls(site_dir: Path) -> list[tuple[str, str]]:
    """Enumerate URLs for every .html page under site_dir.

    Returns a list of (url, lastmod_iso_date) tuples.
    """
    urls = []
    for html in site_dir.rglob('*.html'):
        if html.name in EXCLUDE_HTML_NAMES:
            continue
        # mkdocs emits <page>/index.html with use_directory_urls=true (default).
        # The canonical URL is the containing directory path (with trailing slash).
        # A non-index.html file uses its own filename as the terminal segment.
        parent_rel = html.parent.relative_to(site_dir).as_posix()
        if html.name == 'index.html':
            if parent_rel in ('', '.'):
                url_path = ''
            else:
                url_path = parent_rel + '/'
        else:
            if parent_rel in ('', '.'):
                url_path = html.name
            else:
                url_path = parent_rel + '/' + html.name
        url = SITE_URL + '/' + url_path if url_path else SITE_URL + '/'
        lastmod = datetime.fromtimestamp(html.stat().st_mtime, tz=timezone.utc).strftime('%Y-%m-%d')
        urls.append((url, lastmod))
    return urls


def categorize(urls: list[tuple[str, str]]) -> tuple[dict, dict]:
    """Split URLs into named slices.

    Returns (static_slices, version_slices).
    - static_slices has keys 'main', 'dev', 'stable'
    - version_slices maps 'v<version>' -> [urls]
    """
    static = {'main': [], 'dev': [], 'stable': []}
    versions = {}
    for url, lastmod in urls:
        path = url[len(SITE_URL):].lstrip('/')
        # Drop anything matching an excluded prefix — coverage, legacy
        # mike /dev/*, mkdocs override template leakage.
        if any(path.startswith(p) for p in EXCLUDE_URL_PREFIXES):
            continue
        if path.startswith('api/dev/') or path == 'api/dev/':
            static['dev'].append((url, lastmod))
        elif path.startswith('api/stable/') or path == 'api/stable/':
            static['stable'].append((url, lastmod))
        elif path.startswith('api/v'):
            ver_seg = path.split('/', 2)[1]  # 'v0.0.2-alpha.27'
            versions.setdefault(ver_seg, []).append((url, lastmod))
        else:
            static['main'].append((url, lastmod))
    return static, versions


def write_sitemap(path: Path, entries: list[tuple[str, str]]) -> None:
    if not entries:
        return
    path.parent.mkdir(parents=True, exist_ok=True)
    lines = ['<?xml version="1.0" encoding="UTF-8"?>',
             '<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">']
    for url, lastmod in entries:
        lines.append(f'  <url>')
        lines.append(f'    <loc>{escape(url)}</loc>')
        lines.append(f'    <lastmod>{lastmod}</lastmod>')
        lines.append(f'  </url>')
    lines.append('</urlset>')
    path.write_text('\n'.join(lines), encoding='utf-8')


def fetch_gh_pages_api_dirs() -> list[str]:
    """Return the names of directories under /api/ on gh-pages (e.g. ['dev', 'stable', 'v0.0.2-alpha.19', ...]).

    Falls back to [] on error so the script can still produce a valid (though
    possibly incomplete) sitemap index when running without network access.
    """
    headers = {'Accept': 'application/vnd.github+json'}
    token = os.environ.get('GITHUB_TOKEN')
    if token:
        headers['Authorization'] = f'token {token}'
    try:
        req = urllib.request.Request(GH_PAGES_API, headers=headers)
        with urllib.request.urlopen(req, timeout=30) as r:
            data = json.loads(r.read().decode('utf-8'))
        return sorted(item['name'] for item in data if item.get('type') == 'dir')
    except Exception as e:
        print(f'warn: could not fetch gh-pages /api/ dir list: {e}', file=sys.stderr)
        return []


def write_index(path: Path, sub_sitemap_paths: list[str]) -> None:
    """Write a sitemap index at `path` referencing each sub-sitemap by full URL."""
    lines = ['<?xml version="1.0" encoding="UTF-8"?>',
             '<sitemapindex xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">']
    for sub in sub_sitemap_paths:
        lines.append(f'  <sitemap>')
        lines.append(f'    <loc>{SITE_URL}/{sub}</loc>')
        lines.append(f'  </sitemap>')
    lines.append('</sitemapindex>')
    path.write_text('\n'.join(lines), encoding='utf-8')


def main() -> int:
    args = parse_args()
    site_dir: Path = args.site_dir

    if not site_dir.is_dir():
        print(f'error: {site_dir} is not a directory', file=sys.stderr)
        return 1

    # Remove the stale .gz mkdocs wrote — we're replacing sitemap.xml below
    # and an out-of-date .gz beside it would confuse crawlers.
    gz = site_dir / 'sitemap.xml.gz'
    if gz.exists():
        gz.unlink()

    print(f'Walking {site_dir} for .html files...')
    urls = walk_html_urls(site_dir)
    print(f'Found {len(urls)} URLs')

    static, versions = categorize(urls)
    print(f'  main:    {len(static["main"])}')
    print(f'  dev:     {len(static["dev"])}')
    print(f'  stable:  {len(static["stable"])}')
    for v in sorted(versions):
        print(f'  {v}: {len(versions[v])}')

    # Write per-slice sub-sitemaps into ./site/
    write_sitemap(site_dir / 'sitemap-main.xml',        static['main'])
    write_sitemap(site_dir / 'api' / 'dev'    / 'sitemap.xml', static['dev'])
    write_sitemap(site_dir / 'api' / 'stable' / 'sitemap.xml', static['stable'])
    for v, entries in versions.items():
        write_sitemap(site_dir / 'api' / v / 'sitemap.xml', entries)

    # Build the sitemap index content. Start with slices we JUST wrote.
    local_subs: list[str] = []
    if static['main']:   local_subs.append('sitemap-main.xml')
    if static['dev']:    local_subs.append('api/dev/sitemap.xml')
    if static['stable']: local_subs.append('api/stable/sitemap.xml')
    for v in versions:
        local_subs.append(f'api/{v}/sitemap.xml')

    # Add sub-sitemaps that already exist on gh-pages and we're preserving
    # via keep_files:true. Discovered via the contents API.
    remote_dirs = fetch_gh_pages_api_dirs()
    for d in remote_dirs:
        if d in ('dev', 'stable'):
            sub = f'api/{d}/sitemap.xml'
        elif d.startswith('v'):
            sub = f'api/{d}/sitemap.xml'
        else:
            continue
        if sub not in local_subs:
            local_subs.append(sub)

    # Include the version being released right now (release.yml path).
    if args.current_release:
        sub = f'api/v{args.current_release}/sitemap.xml'
        if sub not in local_subs:
            local_subs.append(sub)

    # Sort for stable output
    local_subs = sorted(set(local_subs))

    write_index(site_dir / 'sitemap.xml', local_subs)
    print(f'Wrote sitemap index with {len(local_subs)} sub-sitemaps')
    for sub in local_subs:
        print(f'  {sub}')

    return 0


if __name__ == '__main__':
    raise SystemExit(main())
