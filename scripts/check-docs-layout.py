"""Check rendered image geometry and footer targets; screenshots are review evidence."""

import argparse
import asyncio
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
import json
from pathlib import Path
from threading import Thread
from urllib.parse import urlsplit

from playwright.async_api import async_playwright


GEOMETRY = """() => ({
    images: [...document.querySelectorAll('article.md-content__inner img')].map(img => {
        const rect = img.getBoundingClientRect();
        return {alt: img.alt, x: rect.x, y: rect.y, width: rect.width, height: rect.height};
    }),
    paragraphs: [...document.querySelectorAll('article.md-content__inner p')].map(p =>
        p.getBoundingClientRect().top),
    footer: [...document.querySelectorAll('.md-copyright__highlight a')].map(a => {
        const rect = a.getBoundingClientRect();
        return {text: a.textContent.trim(), href: a.href, top: rect.top, bottom: rect.bottom,
            width: rect.width, height: rect.height};
    }),
    overflow: document.documentElement.scrollWidth > innerWidth
})"""


async def inspect(browser, origin, output, width, theme, live_images):
    name = f'{"review" if live_images else "fixture"}-{width}-{theme}'
    context = await browser.new_context(
        viewport={'width': width, 'height': 900}, color_scheme=theme,
        reduced_motion='reduce',
    )
    release = asyncio.Event()

    async def route_request(route):
        request = route.request
        local = urlsplit(request.url).netloc == urlsplit(origin).netloc
        if request.resource_type == 'image':
            await release.wait()
            if not local and not live_images:
                await route.fulfill(
                    content_type='image/svg+xml',
                    body='<svg xmlns="http://www.w3.org/2000/svg" width="300" height="20">'
                    '<rect width="300" height="20" fill="#555"/>'
                    '<text x="8" y="14" fill="white" font-size="12">'
                    'Controlled wide badge fixture</text></svg>',
                )
                return
        elif not local and (not live_images or request.resource_type == 'script'):
            await route.abort()
            return
        await route.continue_()

    await context.route('**/*', route_request)
    page = await context.new_page()
    errors = []
    try:
        await page.goto(origin, wait_until='domcontentloaded')
        await page.locator('article.md-content__inner h1').wait_for()
        if theme == 'dark':
            await page.locator('label[title="Switch to dark mode"]').click()
        expected_scheme = 'slate' if theme == 'dark' else 'default'
        await page.wait_for_function(
            'scheme => document.body.dataset.mdColorScheme === scheme', arg=expected_scheme,
        )
        if live_images:
            await asyncio.wait_for(page.evaluate('document.fonts.ready.then(() => true)'), 30)
        await page.evaluate('new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)))')
        before = await page.evaluate(GEOMETRY)
        if not before['images'] or len(before['footer']) < 2:
            errors.append('Expected homepage content images and related-site footer links.')
        if live_images:
            await page.screenshot(path=str(output / f'{name}-reserved.png'))
        release.set()
        await page.wait_for_function(
            '[...document.images].every(img => img.complete && img.naturalWidth > 0)',
        )
        await page.evaluate('new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)))')
        after = await page.evaluate(GEOMETRY)
        for old, new in zip(before['images'], after['images'], strict=True):
            for key in ('x', 'y', 'width', 'height'):
                if abs(old[key] - new[key]) > 0.5:
                    errors.append(f"{old['alt']}: {key} changed from {old[key]} to {new[key]}")
        for index, (old, new) in enumerate(zip(
            before['paragraphs'], after['paragraphs'], strict=True,
        )):
            if abs(old - new) > 0.5:
                errors.append(f'Paragraph {index} moved by {new - old}px after images loaded.')
        for link in after['footer']:
            if link['width'] < 44 or link['height'] < 44:
                errors.append(f"Footer target below 44x44px: {link['text']}: {link['width']}x{link['height']}")
        for previous, following in zip(after['footer'], after['footer'][1:]):
            if following['top'] - previous['bottom'] < 4:
                errors.append('Footer links have less than 4px separation.')
        if after['overflow']:
            errors.append('Horizontal page overflow.')
        if live_images:
            await page.screenshot(path=str(output / f'{name}-loaded.png'))
            await page.locator('.md-footer-meta').screenshot(
                path=str(output / f'{name}-footer.png'),
            )
        return {
            'case': name, 'viewport': {'width': width, 'height': 900}, 'theme': theme,
            'images': 'Real source images' if live_images else 'Controlled wide badge fixtures; real local logo',
            'before': before, 'after': after, 'errors': errors,
        }
    finally:
        release.set()
        await context.close()


async def run(args, origin):
    async with async_playwright() as playwright:
        browser = await playwright.chromium.launch()
        try:
            results = [
                await inspect(browser, origin, args.output, width, theme, args.live_images)
                for width in (375, 1440)
                for theme in ('light', 'dark')
            ]
        finally:
            await browser.close()
    report = {
        'scope': 'Actual MkDocs homepage output. No screenshot assertions or pixel comparisons.',
        'live_images': args.live_images,
        'results': results,
    }
    mode = 'review' if args.live_images else 'fixture'
    (args.output / f'{mode}-geometry.json').write_text(
        json.dumps(report, indent=2), encoding='utf-8',
    )
    if sum(path.stat().st_size for path in args.output.iterdir() if path.is_file()) > 20 * 1024 * 1024:
        raise RuntimeError('Documentation review evidence exceeds its 20 MiB retention budget.')
    for result in results:
        print(result['case'], json.dumps(result['errors']))
    return 1 if any(result['errors'] for result in results) else 0


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--site', type=Path, default=Path('site'))
    parser.add_argument('--output', type=Path, default=Path('artifacts/docs-layout'))
    parser.add_argument('--live-images', action='store_true')
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    server = ThreadingHTTPServer(
        ('127.0.0.1', 0), partial(SimpleHTTPRequestHandler, directory=str(args.site.resolve())),
    )
    worker = Thread(target=server.serve_forever, daemon=True)
    worker.start()
    try:
        return asyncio.run(run(args, f'http://127.0.0.1:{server.server_port}/'))
    finally:
        server.shutdown()
        server.server_close()
        worker.join()


if __name__ == '__main__':
    raise SystemExit(main())
