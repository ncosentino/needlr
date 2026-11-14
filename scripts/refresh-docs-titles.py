"""Refresh only title metadata in previously published API documentation."""

import argparse
from hashlib import sha256
from html import escape, unescape
from html.parser import HTMLParser
import json
from pathlib import Path, PurePosixPath
import re
import sys


sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'docs' / 'hooks'))
from titles import api_title


ATTRIBUTE = re.compile(
    r"""(?P<name>[^\s/<>='"]+)(?:\s*=\s*(?:"(?P<double>[^"]*)"|'(?P<single>[^']*)'|(?P<bare>[^\s>]+)))?""",
)


class TitleSpans(HTMLParser):
    def __init__(self, html):
        super().__init__(convert_charrefs=True)
        self.offsets = [0]
        for line in html.split('\n'):
            self.offsets.append(self.offsets[-1] + len(line) + 1)
        self.spans = {name: [] for name in ('title', 'og:title', 'twitter:title')}
        self.in_head = False
        self.heads = 0
        self.closed_heads = 0
        self.title_start = None

    def position(self):
        line, column = self.getpos()
        return self.offsets[line - 1] + column

    def read_head(self, html):
        for start in range(0, len(html), 4096):
            self.feed(html[start:start + 4096])
            if self.closed_heads:
                break
        self.close()
        self.validate()

    def handle_starttag(self, tag, attrs):
        if tag == 'head':
            self.heads += 1
            self.in_head = True
        if not self.in_head:
            return
        if tag == 'title':
            if self.title_start is not None:
                raise ValueError('Nested head title.')
            self.title_start = self.position() + len(self.get_starttag_text())
        elif tag == 'meta':
            attributes = dict(attrs)
            name = attributes.get('property') or attributes.get('name')
            if name not in ('og:title', 'twitter:title'):
                return
            raw = self.get_starttag_text()
            matches = [
                match for match in ATTRIBUTE.finditer(raw)
                if match.group('name').lower() == 'content'
            ]
            if len(matches) != 1:
                raise ValueError(f'Expected one content attribute for {name}.')
            match = matches[0]
            for group in ('double', 'single', 'bare'):
                if match.group(group) is not None:
                    start, end = match.span(group)
                    self.spans[name].append(
                        (self.position() + start, self.position() + end, group == 'bare'),
                    )
                    break
            else:
                raise ValueError(f'Missing content value for {name}.')

    def handle_endtag(self, tag):
        if not self.in_head:
            return
        if tag == 'title':
            if self.title_start is None:
                raise ValueError('Unmatched head title.')
            self.spans['title'].append((self.title_start, self.position(), False))
            self.title_start = None
        elif tag == 'head':
            self.in_head = False
            self.closed_heads += 1

    def validate(self):
        if self.heads != 1 or self.closed_heads != 1 or self.title_start is not None:
            raise ValueError('Expected one complete HTML head.')
        for name, spans in self.spans.items():
            if len(spans) != 1:
                raise ValueError(f'Expected exactly one head {name}.')


def without_title_values(html: str, parser: TitleSpans) -> str:
    spans = []
    for name, values in parser.spans.items():
        start, end, unquoted = values[0]
        if name != 'title' and not unquoted:
            start -= 1
            end += 1
        spans.append((start, end))
    for start, end in sorted(spans, reverse=True):
        html = html[:start] + html[end:]
    return html


def refresh_html(html: str, source: PurePosixPath) -> str:
    metadata = api_title(source)
    if metadata is None:
        raise ValueError(f'Not an API documentation source: {source}')
    parser = TitleSpans(html)
    parser.read_head(html)
    start, end, _ = parser.spans['title'][0]
    existing = unescape(html[start:end]).strip()
    _, separator, brand = existing.rpartition(' - ')
    if not separator or not brand:
        raise ValueError('Expected a branded MkDocs browser title.')
    titles = {
        'title': f'{metadata} - {brand}',
        'og:title': f'{metadata} \u2013 {brand}',
        'twitter:title': f'{metadata} \u2013 {brand}',
    }
    replacements = []
    for name, spans in parser.spans.items():
        start, end, unquoted = spans[0]
        value = escape(titles[name], quote=True)
        replacements.append((start, end, f'"{value}"' if unquoted else value))
    original = html
    for start, end, value in sorted(replacements, reverse=True):
        html = html[:start] + value + html[end:]
    updated = TitleSpans(html)
    updated.read_head(html)
    if without_title_values(original, parser) != without_title_values(html, updated):
        raise ValueError('Refresh changed bytes outside title metadata values.')
    return html


def source_path(relative: PurePosixPath) -> PurePosixPath:
    if relative.name == 'index.html' and len(relative.parts) > 4:
        return relative.parent.with_name(relative.parent.name + '.md')
    return relative.with_suffix('.md')


def refresh_site(site: Path, *, apply=False) -> dict:
    site = Path(site).resolve()
    api = site / 'api'
    if api.is_symlink() or not (api / 'stable').is_dir():
        raise ValueError('Expected a published site containing api/stable.')
    report = {'scanned': 0, 'changed': 0, 'versions': {}, 'pages': []}
    versions = sorted([api / 'stable', *api.glob('v[0-9]*')])
    for version in versions:
        if version.is_symlink() or not version.is_dir():
            raise ValueError(f'Expected a real API version directory: {version.name}')
        counts = {'scanned': 0, 'changed': 0}
        for path in sorted(version.rglob('*')):
            if path.is_symlink():
                raise ValueError(f'Symlink in published API: {path.relative_to(site)}')
            if not path.is_file() or path.suffix != '.html':
                continue
            relative = PurePosixPath(path.relative_to(site).as_posix())
            source = source_path(relative)
            before = path.read_bytes()
            try:
                after = refresh_html(before.decode('utf-8'), source).encode('utf-8')
            except ValueError as error:
                raise ValueError(f'{relative}: {error}') from error
            counts['scanned'] += 1
            if before != after:
                counts['changed'] += 1
                report['pages'].append({
                    'path': str(relative),
                    'title': api_title(source),
                    'before_sha256': sha256(before).hexdigest(),
                    'after_sha256': sha256(after).hexdigest(),
                })
        if counts['scanned'] == 0:
            raise ValueError(f'No HTML pages found for {version.name}.')
        report['versions'][version.name] = counts
        report['scanned'] += counts['scanned']
        report['changed'] += counts['changed']

    if apply:
        for page in report['pages']:
            path = site / page['path']
            before = path.read_bytes()
            if sha256(before).hexdigest() != page['before_sha256']:
                raise ValueError(f"Snapshot changed before writing {page['path']}.")
            source = source_path(PurePosixPath(page['path']))
            after = refresh_html(before.decode('utf-8'), source).encode('utf-8')
            if sha256(after).hexdigest() != page['after_sha256']:
                raise ValueError(f"Title plan changed for {page['path']}.")
            path.write_bytes(after)
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('site', type=Path)
    parser.add_argument('--apply', action='store_true')
    parser.add_argument('--report', type=Path, required=True)
    args = parser.parse_args()
    if args.report.resolve().is_relative_to(args.site.resolve()):
        parser.error('The report must be outside the published site.')
    report = refresh_site(args.site, apply=args.apply)
    args.report.write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({key: value for key, value in report.items() if key != 'pages'}))


if __name__ == '__main__':
    main()
