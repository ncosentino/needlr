"""Give generated API pages distinct metadata without changing navigation."""

from pathlib import PurePosixPath


def on_page_markdown(markdown: str, page, config, files) -> str:
    path = PurePosixPath(page.file.src_uri)
    parts = path.parts
    if parts[0] != 'api':
        return markdown

    if len(parts) == 2 and path.stem == 'index':
        page.meta['title'] = 'API Reference'
        return markdown
    if len(parts) < 3:
        return markdown

    version = {'dev': 'Development', 'stable': 'Stable'}.get(parts[1], parts[1])
    name = '.'.join(parts[2:-1])
    if path.stem == 'index':
        prefix = f'{name} ' if name else ''
        title = f'{prefix}API Reference ({version})'
    else:
        qualified_name = '.'.join((*parts[2:-1], path.stem))
        title = f'{qualified_name} ({version} API)'

    page.meta['title'] = title
    return markdown
