#!/usr/bin/env python3
"""
Changelog Generator - Analyzes git history to generate Keep a Changelog formatted entries.

Usage:
    python generate.py --from v0.0.1 --to HEAD --version 0.0.2
    python generate.py --from v0.0.1 --to HEAD --version 0.0.2 --mode full --output CHANGELOG.md
"""

import argparse
import re
import subprocess
import sys
from dataclasses import dataclass, field
from datetime import date
from pathlib import Path
from typing import Optional

# Conventional commit type to changelog category mapping
COMMIT_TYPE_MAP = {
    'feat': 'Added',
    'fix': 'Fixed',
    'refactor': 'Changed',
    'perf': 'Changed',
    'style': None,  # Excluded
    'docs': None,   # Excluded
    'test': None,   # Excluded
    'chore': None,  # Excluded
    'ci': None,     # Excluded
    'build': None,  # Excluded
}

# Keywords that indicate breaking changes
BREAKING_KEYWORDS = [
    'BREAKING CHANGE',
    'BREAKING:',
    'breaking change',
    'removes ',
    'removed ',
    'deletes ',
    'deleted ',
    'renames ',
    'renamed ',
]

# File patterns that indicate non-user-facing changes
EXCLUDED_PATTERNS = [
    r'\.md$',
    r'test',
    r'spec',
    r'\.github/',
    r'\.gitignore',
]

CHANGELOG_HEADER = """# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

"""


@dataclass
class Commit:
    """Represents a parsed git commit."""
    hash: str
    subject: str
    body: str
    author: str
    date: str
    files: list[str] = field(default_factory=list)
    commit_type: Optional[str] = None
    scope: Optional[str] = None
    is_breaking: bool = False
    category: Optional[str] = None


@dataclass  
class ChangeEntry:
    """Represents a changelog entry."""
    description: str
    commits: list[str] = field(default_factory=list)
    category: str = 'Changed'


def run_git(args: list[str], repo: Path) -> str:
    """Run a git command and return output."""
    result = subprocess.run(
        ['git'] + args,
        cwd=repo,
        capture_output=True,
        text=True
    )
    if result.returncode != 0:
        raise RuntimeError(f"Git command failed: git {' '.join(args)}\n{result.stderr}")
    return result.stdout


def validate_ref(ref: str, repo: Path) -> bool:
    """Check if a git ref exists."""
    try:
        run_git(['rev-parse', '--verify', ref], repo)
        return True
    except RuntimeError:
        return False


def get_commits(from_ref: str, to_ref: str, repo: Path) -> list[Commit]:
    """Extract commits between two refs."""
    # Use %x00 (NUL) as field separator and a unique record separator
    # Format: hash, subject, body, author, date - separated by NUL
    record_sep = '---END_RECORD---'
    
    output = run_git([
        'log',
        f'{from_ref}..{to_ref}',
        f'--format=%H%x00%s%x00%b%x00%an%x00%ad{record_sep}',
        '--date=short',
        '--name-only'
    ], repo)
    
    commits = []
    raw_commits = output.split(record_sep)
    
    for raw in raw_commits:
        raw = raw.strip()
        if not raw:
            continue
        
        # Split by newlines, first line has the commit info with NUL separators
        lines = raw.split('\n')
        if not lines:
            continue
        
        # Find the line containing commit info (has NUL chars)
        commit_info = None
        file_start_idx = 0
        for i, line in enumerate(lines):
            if '\x00' in line:
                commit_info = line
                file_start_idx = i + 1
                break
        
        if not commit_info:
            continue
            
        # Split by NUL character
        parts = commit_info.split('\x00', 4)
        if len(parts) < 5:
            continue
            
        hash_val, subject, body, author, date_str = parts
        
        # Remaining lines are file names
        files = [f for f in lines[file_start_idx:] if f.strip()]
        
        commit = Commit(
            hash=hash_val[:8],
            subject=subject,
            body=body,
            author=author,
            date=date_str,
            files=files
        )
        
        # Parse conventional commit format
        parse_conventional_commit(commit)
        
        # Detect breaking changes
        detect_breaking_change(commit)
        
        # Assign category
        assign_category(commit)
        
        commits.append(commit)
    
    return commits


def parse_conventional_commit(commit: Commit) -> None:
    """Parse conventional commit format from subject."""
    # Pattern: type(scope)?: description or type: description
    pattern = r'^(\w+)(?:\(([^)]+)\))?(!)?:\s*(.+)$'
    match = re.match(pattern, commit.subject)
    
    if match:
        commit.commit_type = match.group(1).lower()
        commit.scope = match.group(2)
        if match.group(3) == '!':
            commit.is_breaking = True


def detect_breaking_change(commit: Commit) -> None:
    """Detect if commit contains breaking changes."""
    full_text = f"{commit.subject}\n{commit.body}"
    
    for keyword in BREAKING_KEYWORDS:
        if keyword.lower() in full_text.lower():
            commit.is_breaking = True
            return


def assign_category(commit: Commit) -> None:
    """Assign changelog category to commit."""
    if commit.is_breaking:
        commit.category = '⚠️ Breaking Changes'
        return
    
    if commit.commit_type and commit.commit_type in COMMIT_TYPE_MAP:
        commit.category = COMMIT_TYPE_MAP[commit.commit_type]
        return
    
    # Fallback: analyze subject for keywords
    subject_lower = commit.subject.lower()
    
    if any(kw in subject_lower for kw in ['add', 'new', 'create', 'implement']):
        commit.category = 'Added'
    elif any(kw in subject_lower for kw in ['fix', 'bug', 'patch', 'resolve']):
        commit.category = 'Fixed'
    elif any(kw in subject_lower for kw in ['remove', 'delete', 'drop']):
        commit.category = 'Removed'
    elif any(kw in subject_lower for kw in ['deprecate']):
        commit.category = 'Deprecated'
    elif any(kw in subject_lower for kw in ['security', 'cve', 'vulnerability']):
        commit.category = 'Security'
    else:
        commit.category = 'Changed'


def get_net_delta(from_ref: str, to_ref: str, repo: Path) -> dict[str, str]:
    """Get net file changes between refs (A=added, M=modified, D=deleted, R=renamed)."""
    output = run_git([
        'diff',
        '--name-status',
        from_ref,
        to_ref
    ], repo)
    
    delta = {}
    for line in output.strip().split('\n'):
        if not line:
            continue
        parts = line.split('\t')
        if len(parts) >= 2:
            status = parts[0][0]  # First char (R100 -> R)
            filename = parts[-1]  # Last part (for renames, this is the new name)
            delta[filename] = status
    
    return delta


def is_user_facing_file(filename: str) -> bool:
    """Check if file is likely user-facing (not tests, docs, etc.)."""
    for pattern in EXCLUDED_PATTERNS:
        if re.search(pattern, filename, re.IGNORECASE):
            return False
    return True


def deduplicate_commits(commits: list[Commit]) -> list[ChangeEntry]:
    """Group and deduplicate commits into changelog entries."""
    entries_by_category: dict[str, list[ChangeEntry]] = {}
    
    for commit in commits:
        if not commit.category:
            continue
            
        # Skip if all files are non-user-facing
        user_facing_files = [f for f in commit.files if is_user_facing_file(f)]
        if commit.files and not user_facing_files and commit.category != '⚠️ Breaking Changes':
            continue
        
        # Create entry description
        description = commit.subject
        
        # Clean up conventional commit prefix for display
        if commit.commit_type:
            pattern = rf'^{commit.commit_type}(?:\([^)]+\))?!?:\s*'
            description = re.sub(pattern, '', description, flags=re.IGNORECASE)
        
        # Capitalize first letter
        if description:
            description = description[0].upper() + description[1:]
        
        entry = ChangeEntry(
            description=description,
            commits=[commit.hash],
            category=commit.category
        )
        
        if commit.category not in entries_by_category:
            entries_by_category[commit.category] = []
        
        # Check for duplicate descriptions
        existing = next(
            (e for e in entries_by_category[commit.category] 
             if e.description.lower() == entry.description.lower()),
            None
        )
        
        if existing:
            existing.commits.extend(entry.commits)
        else:
            entries_by_category[commit.category].append(entry)
    
    # Flatten to list
    all_entries = []
    for entries in entries_by_category.values():
        all_entries.extend(entries)
    
    return all_entries


def format_changelog_section(
    version: str,
    release_date: str,
    entries: list[ChangeEntry]
) -> str:
    """Format entries as a Keep a Changelog section."""
    lines = [f"## [{version}] - {release_date}", ""]
    
    # Category order
    category_order = [
        '⚠️ Breaking Changes',
        'Added',
        'Changed',
        'Deprecated',
        'Removed',
        'Fixed',
        'Security'
    ]
    
    # Group entries by category
    by_category: dict[str, list[ChangeEntry]] = {}
    for entry in entries:
        if entry.category not in by_category:
            by_category[entry.category] = []
        by_category[entry.category].append(entry)
    
    # Output in order
    for category in category_order:
        if category not in by_category:
            continue
        
        lines.append(f"### {category}")
        for entry in by_category[category]:
            lines.append(f"- {entry.description}")
        lines.append("")
    
    return '\n'.join(lines)


def prepend_to_changelog(
    new_section: str,
    changelog_path: Path
) -> str:
    """Prepend new section to existing CHANGELOG.md."""
    if not changelog_path.exists():
        return CHANGELOG_HEADER + new_section
    
    content = changelog_path.read_text(encoding='utf-8')
    
    # Find insertion point (after header, before first ## [)
    # Look for first version header
    match = re.search(r'^## \[', content, re.MULTILINE)
    
    if match:
        # Insert before first version
        insert_pos = match.start()
        return content[:insert_pos] + new_section + content[insert_pos:]
    else:
        # No versions yet, append after header
        return content.rstrip() + '\n\n' + new_section


def main():
    parser = argparse.ArgumentParser(
        description='Generate changelog entries from git history'
    )
    parser.add_argument(
        '--from', 
        dest='from_ref',
        required=True,
        help='Starting ref (exclusive) - tag, commit, or branch'
    )
    parser.add_argument(
        '--to',
        dest='to_ref', 
        default='HEAD',
        help='Ending ref (inclusive), default: HEAD'
    )
    parser.add_argument(
        '--version',
        required=True,
        help='Version string for the header (e.g., "1.2.0")'
    )
    parser.add_argument(
        '--date',
        default=date.today().isoformat(),
        help='Release date in YYYY-MM-DD format, default: today'
    )
    parser.add_argument(
        '--repo',
        type=Path,
        default=Path('.'),
        help='Path to git repository, default: current directory'
    )
    parser.add_argument(
        '--output',
        type=Path,
        help='Output file path, default: stdout'
    )
    parser.add_argument(
        '--mode',
        choices=['section', 'full'],
        default='section',
        help='section: just this version, full: prepend to existing CHANGELOG.md'
    )
    
    args = parser.parse_args()
    
    # Validate refs
    if not validate_ref(args.from_ref, args.repo):
        print(f"Error: ref '{args.from_ref}' does not exist", file=sys.stderr)
        sys.exit(1)
    
    if not validate_ref(args.to_ref, args.repo):
        print(f"Error: ref '{args.to_ref}' does not exist", file=sys.stderr)
        sys.exit(1)
    
    # Get commits
    commits = get_commits(args.from_ref, args.to_ref, args.repo)
    
    if not commits:
        print(f"Warning: no commits found between {args.from_ref} and {args.to_ref}", 
              file=sys.stderr)
    
    # Get net delta for context (not currently used, but available for enhancement)
    # delta = get_net_delta(args.from_ref, args.to_ref, args.repo)
    
    # Deduplicate and categorize
    entries = deduplicate_commits(commits)
    
    # Format section
    section = format_changelog_section(args.version, args.date, entries)
    
    # Output
    if args.mode == 'full' and args.output:
        result = prepend_to_changelog(section, args.output)
        args.output.write_text(result, encoding='utf-8')
        print(f"Updated {args.output}", file=sys.stderr)
    elif args.output:
        args.output.write_text(section, encoding='utf-8')
        print(f"Wrote {args.output}", file=sys.stderr)
    else:
        print(section)


if __name__ == '__main__':
    main()
