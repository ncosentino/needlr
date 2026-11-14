"""Validate rendered API titles without changing visible headings or navigation."""

from html.parser import HTMLParser
import importlib.util
from pathlib import Path, PurePosixPath
import tempfile
import unittest

from mkdocs.commands.build import build
from mkdocs.config import load_config


ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location(
    'refresh_docs_titles', ROOT / 'scripts' / 'refresh-docs-titles.py',
)
refresh = importlib.util.module_from_spec(spec)
spec.loader.exec_module(refresh)


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
            'api/index.md': '# API Reference\n',
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
            nav=[
                {'Overview': source} if source == 'api/index.md' else source
                for source in pages
            ],
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

    def test_api_overview_title_is_not_the_generic_navigation_label(self):
        page = self.assert_api_title('api/index.md', 'API Reference')
        self.assertEqual(page.headings, ['API Reference'])

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


class PublishedTitlesRefreshTests(unittest.TestCase):
    def html(self):
        return (
            '<!doctype html>\r\n<html><head>\r\n'
            '<title>Index - Needlr</title>\r\n'
            '<meta property="og:title" content="Index \u2013 Needlr" />\r\n'
            '<meta name="twitter:title" content=\'Index \u2013 Needlr\' />\r\n'
            '<meta name="description" content="Original API description" />\r\n'
            '<link rel="canonical" href="https://example.test/api/old/" />\r\n'
            '<script type="application/ld+json">{"name":"Original API"}</script>\r\n'
            '</head><body><h1>Index</h1><nav>Original navigation</nav>\r\n'
            '<code>Original version: Factory&lt;T&gt; \u03bb</code>\r\n'
            '<svg><title>Unchanged diagram title</title></svg>\r\n'
            '</body></html>\r\n'
        )

    def test_refresh_changes_only_the_three_metadata_values(self):
        original = self.html()
        title = 'NexusLabs.Needlr API Reference (Stable)'
        expected = original.replace('Index - Needlr', f'{title} - Needlr')
        expected = expected.replace('Index \u2013 Needlr', f'{title} \u2013 Needlr')
        self.assertEqual(
            refresh.refresh_html(
                original, PurePosixPath('api/stable/NexusLabs.Needlr/index.md'),
            ),
            expected,
        )

    def test_attribute_values_are_escaped_without_matching_nested_text(self):
        original = self.html().replace(
            'property="og:title"',
            'data-note=\'content="not a title"\' property="og:title"',
        )
        result = refresh.refresh_html(
            original, PurePosixPath('api/v1.0/NexusLabs.Needlr/Factory&T.md'),
        )
        parser = MetadataParser()
        parser.feed(result.split('</head>', 1)[0])
        expected = 'NexusLabs.Needlr.Factory&T (v1.0 API)'
        self.assertEqual(parser.title, f'{expected} - Needlr')
        self.assertEqual(parser.meta['og:title'], f'{expected} \u2013 Needlr')
        self.assertIn('data-note=\'content="not a title"\'', result)
        self.assertIn('Factory&amp;T', result)

    def test_missing_or_duplicate_metadata_fails_loudly(self):
        for malformed in (
            self.html().replace('<title>Index - Needlr</title>', ''),
            self.html().replace(
                '<meta name="twitter:title" content=\'Index \u2013 Needlr\' />', '',
            ),
            self.html().replace(
                '</head>', '<meta property="og:title" content="duplicate"></head>',
            ),
        ):
            with self.subTest(html=malformed), self.assertRaises(ValueError):
                refresh.refresh_html(
                    malformed, PurePosixPath('api/stable/NexusLabs.Needlr/index.md'),
                )

    def make_site(self, root):
        site = Path(root)
        sources = (
            'api/stable/NexusLabs.Needlr/index.html',
            'api/v0.0.2-alpha.19/NexusLabs.Needlr/Factory_T_.Nested/index.html',
            'api/dev/NexusLabs.Needlr/index.html',
            'getting-started/index.html',
        )
        for source in sources:
            path = site / source
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(self.html().encode('utf-8'))
        return site, sources

    def test_preview_apply_and_repeat_preserve_version_content_and_other_slices(self):
        with tempfile.TemporaryDirectory() as directory:
            site, sources = self.make_site(directory)
            before = {source: (site / source).read_bytes() for source in sources}
            preview = refresh.refresh_site(site)
            self.assertEqual(preview['scanned'], 2)
            self.assertEqual(preview['changed'], 2)
            for source in sources:
                self.assertEqual((site / source).read_bytes(), before[source])

            applied = refresh.refresh_site(site, apply=True)
            self.assertEqual(applied, preview)
            for source, expected in (
                (sources[0], 'NexusLabs.Needlr API Reference (Stable)'),
                (sources[1], 'NexusLabs.Needlr.Factory_T_.Nested (v0.0.2-alpha.19 API)'),
            ):
                result = (site / source).read_bytes()
                expected_html = before[source].decode('utf-8')
                expected_html = expected_html.replace(
                    'Index - Needlr', f'{expected} - Needlr',
                ).replace('Index \u2013 Needlr', f'{expected} \u2013 Needlr')
                self.assertEqual(result, expected_html.encode('utf-8'))
            for source in sources[2:]:
                self.assertEqual((site / source).read_bytes(), before[source])
            self.assertEqual(refresh.refresh_site(site)['changed'], 0)

    def test_every_page_is_validated_before_any_file_is_changed(self):
        with tempfile.TemporaryDirectory() as directory:
            site, sources = self.make_site(directory)
            valid = site / sources[0]
            malformed = site / sources[1]
            before = valid.read_bytes()
            malformed.write_text('<html><head></head></html>', encoding='utf-8')
            with self.assertRaises(ValueError):
                refresh.refresh_site(site, apply=True)
            self.assertEqual(valid.read_bytes(), before)

    def test_symlinks_cannot_redirect_refresh_writes(self):
        with tempfile.TemporaryDirectory() as directory:
            site, sources = self.make_site(Path(directory) / 'site')
            outside = Path(directory) / 'outside.html'
            outside.write_bytes(self.html().encode('utf-8'))
            target = site / sources[0]
            target.unlink()
            target.symlink_to(outside)
            before = outside.read_bytes()
            with self.assertRaises(ValueError):
                refresh.refresh_site(site, apply=True)
            self.assertEqual(outside.read_bytes(), before)


if __name__ == '__main__':
    unittest.main()
