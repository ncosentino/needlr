"""Give generated API pages distinct metadata without changing navigation."""

from pathlib import PurePosixPath


def on_page_markdown(markdown: str, page, config, files) -> str:
    path = PurePosixPath(page.file.src_uri)
    parts = path.parts
    if len(parts) < 4 or parts[0] != 'api':
        return markdown

    version = {'dev': 'Development', 'stable': 'Stable'}.get(parts[1], parts[1])
    name = '.'.join(parts[2:-1])
    if path.stem == 'index':
        title = f'{name} API Reference ({version})'
    else:
        title = f'{name}.{path.stem} ({version} API)'

    page.meta['title'] = title
    return markdown
