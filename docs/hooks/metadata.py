"""Derive documentation metadata without changing source or rendered content."""

from copy import copy
from html.parser import HTMLParser
from pathlib import PurePosixPath

MAX_DESCRIPTION_LENGTH = 240
VOID_TAGS = {
    'area', 'base', 'br', 'col', 'embed', 'hr', 'img', 'input',
    'link', 'meta', 'param', 'source', 'track', 'wbr',
}
EXCLUDED_TAGS = {
    'aside', 'blockquote', 'details', 'figure', 'form', 'nav',
    'ol', 'pre', 'script', 'style', 'table', 'ul',
    'h1', 'h2', 'h3', 'h4', 'h5', 'h6',
}


def normalize(text: str) -> str:
    return ' '.join(text.split())


class IntroductionParser(HTMLParser):
    def __init__(self, *, api: bool):
        super().__init__(convert_charrefs=True)
        self.api = api
        self.introduction = ''
        self.paragraph = None
        self.skipping = 0
        self.type_heading = False
        self.finished = False

    def handle_starttag(self, tag, attrs):
        if self.finished:
            return
        if tag in VOID_TAGS:
            if tag == 'br' and self.paragraph is not None and not self.skipping:
                self.paragraph.append(' ')
            return
        if self.skipping:
            self.skipping += 1
            return
        if self.api:
            if tag in ('pre', 'table'):
                self.finished = True
                return
            if tag == 'h2':
                if self.type_heading:
                    self.finished = True
                    return
                self.type_heading = True
            elif self.type_heading and tag in ('h3', 'h4', 'h5', 'h6'):
                self.finished = True
                return
        classes = (dict(attrs).get('class') or '').split()
        if tag in EXCLUDED_TAGS or 'admonition' in classes:
            self.skipping = 1
        elif tag == 'p':
            self.paragraph = []

    def handle_startendtag(self, tag, attrs):
        self.handle_starttag(tag, attrs)
        if tag not in VOID_TAGS:
            self.handle_endtag(tag)

    def handle_endtag(self, tag):
        if self.finished:
            return
        if self.skipping:
            self.skipping -= 1
        elif tag == 'p' and self.paragraph is not None:
            self.introduction = normalize(''.join(self.paragraph))
            self.paragraph = None
            self.finished = bool(self.introduction)

    def handle_data(self, data):
        if not self.finished and not self.skipping and self.paragraph is not None:
            self.paragraph.append(data)


def shorten_description(text: str) -> str:
    if len(text) <= MAX_DESCRIPTION_LENGTH:
        return text
    end = text.rfind(' ', 0, MAX_DESCRIPTION_LENGTH - 2)
    return text[:end].rstrip() + '...' if end > 0 else text


def api_title(path: PurePosixPath) -> str | None:
    parts = path.parts
    if not parts or parts[0] != 'api':
        return None

    if len(parts) == 2 and path.stem == 'index':
        return 'API Reference'
    if len(parts) < 3:
        return None

    version = {'dev': 'Development', 'stable': 'Stable'}.get(parts[1], parts[1])
    name = '.'.join(parts[2:-1])
    if path.stem == 'index':
        prefix = f'{name} ' if name else ''
        title = f'{prefix}API Reference ({version})'
    else:
        qualified_name = '.'.join((*parts[2:-1], path.stem))
        title = f'{qualified_name} ({version} API)'

    return title


def on_page_markdown(markdown: str, page, config, files) -> str:
    title = api_title(PurePosixPath(page.file.src_uri))
    if title is not None:
        page.meta['title'] = title
        if page.canonical_url is not None:
            page.canonical_url = page.canonical_url.rstrip('/')
    return markdown


def page_description(content: str, source: PurePosixPath, title: str, site_name: str) -> str:
    reference = api_title(source)
    parser = IntroductionParser(api=reference is not None)
    parser.feed(content)
    parser.close()
    identity = reference or normalize(title)
    if parser.introduction:
        return shorten_description(f'{identity}. {parser.introduction}')
    return f'{site_name}: {identity}.'


def on_page_content(content: str, page, config, files) -> str:
    authored = page.meta.get('description')
    if authored is not None and not isinstance(authored, str):
        raise ValueError(f'{page.file.src_uri}: description must be a string.')
    description = normalize(authored) if authored is not None else ''
    if not description:
        if page.is_homepage and config.site_description:
            description = normalize(config.site_description)
        else:
            description = page_description(
                content, PurePosixPath(page.file.src_uri), page.title, config.site_name,
            )
    page.meta['description'] = description
    return content


def on_page_context(context, page, config, nav):
    from markupsafe import escape

    # MkDocs does not autoescape templates; keep escaped values out of source metadata.
    rendered_page = copy(page)
    rendered_page.meta = {**page.meta, 'description': escape(page.meta['description'])}
    context['page'] = rendered_page
    return context
