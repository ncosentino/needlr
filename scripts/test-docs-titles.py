"""Validate rendered API titles without changing visible headings or navigation."""

from html.parser import HTMLParser
from pathlib import Path
import tempfile
import unittest

from mkdocs.commands.build import build
from mkdocs.config import load_config


ROOT = Path(__file__).resolve().parents[1]


class MetadataParser(HTMLParser):
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.title = ''
        self.headings = []
        self.meta = {}
        self._in_title = False
        self._in_heading = False
        self._in_permalink = False

    def handle_starttag(self, tag, attrs):
        attributes = dict(attrs)
        if tag == 'title':
            self._in_title = True
        elif tag == 'h1':
            self._in_heading = True
            self.headings.append('')
        elif tag == 'a' and 'headerlink' in attributes.get('class', '').split():
            self._in_permalink = True
        elif tag == 'meta':
            name = attributes.get('property') or attributes.get('name')
            self.meta[name] = attributes.get('content')

    def handle_endtag(self, tag):
        if tag == 'title':
            self._in_title = False
        elif tag == 'h1':
            self._in_heading = False
        elif tag == 'a':
            self._in_permalink = False

    def handle_data(self, data):
        if self._in_title:
            self.title += data
        if self._in_heading and not self._in_permalink:
            self.headings[-1] += data


class DocumentationTitlesTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.workspace = tempfile.TemporaryDirectory()
        cls.addClassCleanup(cls.workspace.cleanup)
        root = Path(cls.workspace.name)
        docs = root / 'docs'
        cls.site = root / 'site'
        pages = {
            'index.md': '# Needlr\n',
            'getting-started.md': '# Getting Started\n',
            'api/dev/index.md': '# API Reference (Development)\n',
            'api/dev/NexusLabs.Needlr/index.md': '# Index\n',
            'api/stable/NexusLabs.Needlr/index.md': '# Index\n',
            'api/v0.0.3-alpha.8/NexusLabs.Needlr/index.md': '# Index\n',
            'api/dev/NexusLabs.Needlr/Plugin.md': '# Plugin\n',
            'api/dev/NexusLabs.Needlr.AspNet/Plugin.md': '# Plugin\n',
            'api/stable/NexusLabs.Needlr/Plugin.md': '# Plugin\n',
            'api/dev/NexusLabs.Needlr/Factory_T_.md': '# Factory<T>\n',
        }
        for source, markdown in pages.items():
            path = docs / source
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(markdown, encoding='utf-8')

        config = load_config(
            config_file=str(ROOT / 'mkdocs.yml'),
            docs_dir=str(docs),
            site_dir=str(cls.site),
            site_url='https://example.test/reference/',
            nav=list(pages),
            extra_javascript=[],
        )
        build(config)

    def read_page(self, source):
        path = self.site / source.removesuffix('.md') / 'index.html'
        if source.endswith('/index.md'):
            path = self.site / source
            path = path.with_suffix('.html')
        parser = MetadataParser()
        parser.feed(path.read_text(encoding='utf-8'))
        return parser

    def assert_api_title(self, source, expected):
        page = self.read_page(source)
        self.assertEqual(page.title.strip(), f'{expected} - Needlr')
        self.assertEqual(page.meta['og:title'], f'{expected} – Needlr')
        self.assertEqual(page.meta['twitter:title'], page.meta['og:title'])
        return page

    def test_namespace_indexes_include_package_and_version(self):
        for version, label in (
            ('dev', 'Development'),
            ('stable', 'Stable'),
            ('v0.0.3-alpha.8', 'v0.0.3-alpha.8'),
        ):
            with self.subTest(version=version):
                page = self.assert_api_title(
                    f'api/{version}/NexusLabs.Needlr/index.md',
                    f'NexusLabs.Needlr API Reference ({label})',
                )
                self.assertEqual(page.headings, ['Index'])

    def test_same_named_types_include_package_and_version(self):
        for version, package, label in (
            ('dev', 'NexusLabs.Needlr', 'Development'),
            ('dev', 'NexusLabs.Needlr.AspNet', 'Development'),
            ('stable', 'NexusLabs.Needlr', 'Stable'),
        ):
            with self.subTest(version=version, package=package):
                page = self.assert_api_title(
                    f'api/{version}/{package}/Plugin.md',
                    f'{package}.Plugin ({label} API)',
                )
                self.assertEqual(page.headings, ['Plugin'])

    def test_generic_type_filename_is_unambiguous_in_metadata(self):
        self.assert_api_title(
            'api/dev/NexusLabs.Needlr/Factory_T_.md',
            'NexusLabs.Needlr.Factory_T_ (Development API)',
        )

    def test_guide_and_api_catalog_titles_are_unchanged(self):
        for source, expected in (
            ('getting-started.md', 'Getting Started'),
            ('api/dev/index.md', 'API Reference (Development)'),
        ):
            with self.subTest(source=source):
                page = self.read_page(source)
                self.assertEqual(page.title.strip(), f'{expected} - Needlr')
                self.assertEqual(page.headings, [expected])


if __name__ == '__main__':
    unittest.main()
