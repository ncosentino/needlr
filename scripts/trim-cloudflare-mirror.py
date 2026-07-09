#!/usr/bin/env python3
"""Trim the gh-pages mirror before deploying it to the canonical (Cloudflare)
host, which enforces a per-deployment file-count limit.

The per-version API doc archives under api/v* are numerous (hundreds of files
each) and would exceed the limit if all were included. This keeps only the
newest N archives and rewrites api/versions.json so the version switcher only
offers versions that are actually hosted. Older archives remain in the
gh-pages branch history; they are simply not published to the canonical host
(the projects-router redirects their URLs to stable).

Version entries in versions.json are ordered newest-first by the generator, so
"newest N" is just the first N version entries — no version parsing required.

Usage:
    python scripts/trim-cloudflare-mirror.py <mirror-dir> [--keep N]
"""
from __future__ import annotations

import argparse
import json
import shutil
import sys
from pathlib import Path


def is_version_name(name: str) -> bool:
    """True for archive directory/entry names like 'v0.0.2-alpha.66'."""
    return len(name) >= 2 and name[0] == 'v' and name[1].isdigit()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument('mirror_dir', type=Path, help='Path to the gh-pages mirror checkout')
    parser.add_argument('--keep', type=int, default=20,
                        help='Number of newest api/v* archives to keep (default: 20)')
    args = parser.parse_args()

    mirror: Path = args.mirror_dir
    if not mirror.is_dir():
        print(f'error: {mirror} is not a directory', file=sys.stderr)
        return 1

    # Drop the checkout's git metadata so it is not uploaded to the host.
    git_dir = mirror / '.git'
    if git_dir.exists():
        shutil.rmtree(git_dir)

    api_dir = mirror / 'api'
    versions_file = api_dir / 'versions.json'

    keep_paths: set[str] = set()
    if versions_file.is_file():
        data = json.loads(versions_file.read_text(encoding='utf-8'))
        entries = data.get('entries', [])

        kept_count = 0
        new_entries = []
        for entry in entries:
            path = str(entry.get('path', ''))
            if not entry.get('separator') and is_version_name(path):
                if kept_count < args.keep:
                    new_entries.append(entry)
                    keep_paths.add(path)
                    kept_count += 1
            else:
                new_entries.append(entry)

        # Avoid a dangling separator left above an emptied version list.
        while new_entries and new_entries[-1].get('separator'):
            new_entries.pop()

        data['entries'] = new_entries
        versions_file.write_text(json.dumps(data, indent=2) + '\n', encoding='utf-8')
    else:
        print(f'warn: {versions_file} not found; removing all api/v* archives', file=sys.stderr)

    removed = 0
    if api_dir.is_dir():
        for child in api_dir.iterdir():
            if child.is_dir() and is_version_name(child.name) and child.name not in keep_paths:
                shutil.rmtree(child)
                removed += 1

    print(f'Kept {len(keep_paths)} version archive(s); removed {removed} older archive dir(s).')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
