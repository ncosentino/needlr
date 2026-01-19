#!/usr/bin/env python3
"""
Changelog Generator - Analyzes git diff to generate semantic changelog entries.

This skill analyzes the ACTUAL code delta between two refs, not just commit messages.
It examines file changes, understands patterns, and generates meaningful summaries.

Usage:
    python generate.py --from v0.0.1 --to HEAD --version 0.0.2
"""

import argparse
import re
import subprocess
import sys
from collections import defaultdict
from dataclasses import dataclass, field
from datetime import date
from pathlib import Path
from typing import Optional


@dataclass
class FileChange:
    """Represents a changed file."""
    path: str
    status: str  # A=added, M=modified, D=deleted, R=renamed
    insertions: int = 0
    deletions: int = 0
    old_path: Optional[str] = None  # For renames


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


def get_file_changes(from_ref: str, to_ref: str, repo: Path) -> list[FileChange]:
    """Get all file changes between two refs."""
    # Get numstat for insertions/deletions
    numstat_output = run_git([
        'diff', '--numstat', '-M',
        f'{from_ref}..{to_ref}'
    ], repo)
    
    stats = {}
    for line in numstat_output.strip().split('\n'):
        if not line:
            continue
        parts = line.split('\t')
        if len(parts) >= 3:
            ins = int(parts[0]) if parts[0] != '-' else 0
            dels = int(parts[1]) if parts[1] != '-' else 0
            filepath = parts[-1]
            stats[filepath] = (ins, dels)
    
    # Get name-status for change types
    status_output = run_git([
        'diff', '--name-status', '-M',
        f'{from_ref}..{to_ref}'
    ], repo)
    
    changes = []
    for line in status_output.strip().split('\n'):
        if not line:
            continue
        parts = line.split('\t')
        if len(parts) >= 2:
            status = parts[0][0]  # First char (R100 -> R)
            
            if status == 'R' and len(parts) >= 3:
                old_path = parts[1]
                new_path = parts[2]
                ins, dels = stats.get(new_path, (0, 0))
                changes.append(FileChange(new_path, status, ins, dels, old_path))
            else:
                filepath = parts[-1]
                ins, dels = stats.get(filepath, (0, 0))
                changes.append(FileChange(filepath, status, ins, dels))
    
    return changes


def categorize_path(path: str) -> str:
    """Categorize a file path into a logical area."""
    path_lower = path.lower()
    
    if '.github/' in path_lower:
        return 'ci'
    if '/test' in path_lower or path_lower.endswith('tests.cs'):
        return 'tests'
    if '/benchmark' in path_lower:
        return 'benchmarks'
    if '/example' in path_lower:
        return 'examples'
    if '/docs/' in path_lower or (path_lower.endswith('.md') and '/src/' not in path_lower):
        return 'docs'
    if '/analyzers/' in path_lower or 'analyzer' in path_lower:
        return 'analyzers'
    if '/skills/' in path_lower:
        return 'skills'
    if 'signalr' in path_lower:
        return 'signalr'
    if 'semantickernel' in path_lower:
        return 'semantickernel'
    if 'sourcegen' in path_lower or 'generator' in path_lower:
        return 'sourcegen'
    if '.reflection' in path_lower:
        return 'reflection'
    if 'scrutor' in path_lower:
        return 'scrutor'
    if '.bundle' in path_lower:
        return 'bundle'
    if 'aspnet' in path_lower or 'webapp' in path_lower:
        return 'aspnet'
    if '.injection' in path_lower:
        return 'injection'
    if 'syringe' in path_lower:
        return 'syringe'
    if '/scripts/' in path_lower:
        return 'scripts'
    
    # Core Needlr package
    if 'nexuslabs.needlr/' in path_lower and '.cs' in path_lower:
        return 'core'
    
    return 'other'


def analyze_changes(changes: list[FileChange]) -> dict:
    """Analyze file changes to understand what happened."""
    analysis = {
        'added_files': [],
        'deleted_files': [],
        'modified_files': [],
        'renamed_files': [],
        'by_category': defaultdict(list),
        'stats': {
            'total_files': len(changes),
            'insertions': sum(c.insertions for c in changes),
            'deletions': sum(c.deletions for c in changes),
        }
    }
    
    for change in changes:
        category = categorize_path(change.path)
        analysis['by_category'][category].append(change)
        
        if change.status == 'A':
            analysis['added_files'].append(change)
        elif change.status == 'D':
            analysis['deleted_files'].append(change)
        elif change.status == 'R':
            analysis['renamed_files'].append(change)
        else:
            analysis['modified_files'].append(change)
    
    return analysis


def detect_deleted_interfaces(changes: list[FileChange]) -> list[str]:
    """Detect deleted public interfaces."""
    deleted_interfaces = []
    for change in changes:
        if change.status == 'D' and change.path.endswith('.cs'):
            filename = Path(change.path).stem
            if filename.startswith('I') and len(filename) > 1 and filename[1].isupper():
                deleted_interfaces.append(filename)
    return deleted_interfaces


def detect_new_projects(changes: list[FileChange]) -> list[str]:
    """Detect new .csproj files added."""
    new_projects = []
    for change in changes:
        if change.status == 'A' and change.path.endswith('.csproj'):
            name = Path(change.path).stem
            new_projects.append(name)
    return new_projects


def detect_renames(changes: list[FileChange]) -> list[tuple[str, str]]:
    """Detect renamed files and return (old_name, new_name) tuples."""
    renames = []
    for change in changes:
        if change.status == 'R' and change.old_path and change.path.endswith('.cs'):
            old_name = Path(change.old_path).stem
            new_name = Path(change.path).stem
            if old_name != new_name:
                renames.append((old_name, new_name))
    return renames


def detect_deleted_classes(changes: list[FileChange]) -> list[str]:
    """Detect deleted .cs files (not interfaces)."""
    deleted = []
    for change in changes:
        if change.status == 'D' and change.path.endswith('.cs'):
            filename = Path(change.path).stem
            # Skip interfaces (already handled by detect_deleted_interfaces)
            if not (filename.startswith('I') and len(filename) > 1 and filename[1].isupper()):
                deleted.append(filename)
    return deleted


def generate_semantic_changelog(
    analysis: dict,
    changes: list[FileChange],
    repo: Path,
    from_ref: str,
    to_ref: str,
    version: str,
    release_date: str
) -> str:
    """Generate a semantic changelog from analysis."""
    
    lines = [f"## [{version}] - {release_date}", ""]
    
    breaking_changes = []
    added = []
    changed = []
    removed = []
    
    categories = analysis['by_category']
    
    # Detect breaking changes - deleted interfaces
    deleted_interfaces = detect_deleted_interfaces(changes)
    for iface in deleted_interfaces:
        breaking_changes.append(f"Removed `{iface}` interface")
    
    # Detect renames - these are often breaking
    renames = detect_renames(changes)
    for old_name, new_name in renames:
        # Check if this looks like a significant rename
        if 'Factory' in old_name or 'Provider' in old_name or 'Registrar' in old_name:
            changed.append(f"`{old_name}` renamed to `{new_name}`")
    
    # Detect deleted classes (may be breaking or just removals)
    deleted_classes = detect_deleted_classes(changes)
    
    # Core package deletions are breaking
    core_deletions = [c for c in changes if c.status == 'D' and 'NexusLabs.Needlr/' in c.path and c.path.endswith('.cs')]
    for change in core_deletions:
        name = Path(change.path).stem
        if name not in [iface for iface in deleted_interfaces]:  # Don't double-report interfaces
            if 'Factory' in name or 'Provider' in name or 'Registrar' in name or 'Populator' in name:
                removed.append(f"`{name}` (moved to explicit package or replaced)")
    
    # Source Generation - the big theme
    if 'sourcegen' in categories:
        sg_files = categories['sourcegen']
        sg_added = [f for f in sg_files if f.status == 'A']
        sg_modified = [f for f in sg_files if f.status == 'M']
        sg_deleted = [f for f in sg_files if f.status == 'D']
        
        if sg_added:
            # Check what was added
            added_names = [Path(f.path).stem for f in sg_added if f.path.endswith('.cs')]
            if any('provider' in n.lower() for n in added_names):
                added.append("Source-generation type providers (`GeneratedTypeProvider`, `GeneratedPluginProvider`)")
            if any('bootstrap' in n.lower() for n in added_names):
                added.append("Module initializer bootstrap for zero-reflection startup")
            if any('factory' in n.lower() for n in added_names):
                added.append("Source-generation plugin factory (`GeneratedPluginFactory`)")
        
        if sg_modified or sg_added:
            changed.append("Source generation is now the default pattern (reflection is opt-in)")
    
    # Reflection package
    if 'reflection' in categories:
        ref_files = categories['reflection']
        ref_added = [f for f in ref_files if f.status == 'A']
        if ref_added:
            added_names = [Path(f.path).stem for f in ref_added if f.path.endswith('.cs')]
            if any('provider' in n.lower() for n in added_names):
                added.append("Reflection-based type providers (`ReflectionTypeProvider`, `ReflectionPluginProvider`)")
            if any('factory' in n.lower() for n in added_names):
                added.append("Reflection-based plugin factory (`ReflectionPluginFactory`)")
    
    # Bundle package
    if 'bundle' in categories:
        bundle_files = categories['bundle']
        bundle_added = [f for f in bundle_files if f.status == 'A']
        if bundle_added:
            added.append("Bundle package with auto-configuration (source-gen first, reflection fallback)")
    
    # Analyzers
    if 'analyzers' in categories:
        analyzer_files = categories['analyzers']
        analyzer_added = [f for f in analyzer_files if f.status == 'A' and f.path.endswith('.cs')]
        if analyzer_added:
            # Find analyzer IDs from file content or names
            analyzer_ids = set()
            for f in analyzer_added:
                # Check for NDLR pattern in path
                match = re.search(r'NDLR\d+', f.path)
                if match:
                    analyzer_ids.add(match.group())
            if analyzer_ids:
                added.append(f"Roslyn analyzers ({', '.join(sorted(analyzer_ids))})")
            else:
                added.append("Roslyn analyzers for common Needlr mistakes")
    
    # SignalR
    if 'signalr' in categories:
        sr_files = categories['signalr']
        sr_added = [f for f in sr_files if f.status == 'A' and f.path.endswith('.cs')]
        sr_modified = [f for f in sr_files if f.status == 'M']
        if any('generated' in f.path.lower() for f in sr_added):
            added.append("Source-generation support for SignalR hub registration")
        if sr_modified:
            changed.append("SignalR now accepts `IPluginFactory` via dependency injection (no hardcoded reflection)")
    
    # SemanticKernel
    if 'semantickernel' in categories:
        sk_files = categories['semantickernel']
        sk_added = [f for f in sk_files if f.status == 'A' and f.path.endswith('.cs')]
        sk_modified = [f for f in sk_files if f.status == 'M']
        if any('generated' in f.path.lower() for f in sk_added):
            added.append("Source-generation support for Semantic Kernel plugin discovery")
        if sk_modified:
            changed.append("Semantic Kernel now accepts `IPluginFactory` via dependency injection")
    
    # ASP.NET
    if 'aspnet' in categories:
        aspnet_files = categories['aspnet']
        aspnet_modified = [f for f in aspnet_files if f.status == 'M']
        if aspnet_modified:
            changed.append("ASP.NET package decoupled from Bundle (explicit strategy choice required)")
    
    # Core injection changes
    if 'injection' in categories or 'syringe' in categories:
        core_files = categories.get('injection', []) + categories.get('syringe', [])
        core_modified = [f for f in core_files if f.status == 'M']
        core_deleted = [f for f in core_files if f.status == 'D']
        core_added = [f for f in core_files if f.status == 'A']
        
        if core_added:
            added_names = [Path(f.path).stem for f in core_added if f.path.endswith('.cs')]
            if any('provider' in n.lower() for n in added_names):
                added.append("Provider-based architecture (`IInjectableTypeProvider`, `IPluginTypeProvider`)")
        
        if core_modified:
            total_churn = sum(f.insertions + f.deletions for f in core_modified)
            if total_churn > 200:
                changed.append("Simplified Syringe API with provider-based architecture")
        
        if core_deleted:
            for f in core_deleted:
                if f.path.endswith('.cs'):
                    name = Path(f.path).stem
                    path_lower = f.path.lower()
                    if 'loader' in name.lower():
                        removed.append(f"`{name}` (assembly loading now handled by providers)")
                    elif 'sorter' in name.lower():
                        removed.append(f"`{name}` (assembly sorting no longer needed)")
                    elif 'registrar' in name.lower():
                        removed.append(f"`{name}` (replaced by `IInjectableTypeProvider`)")
                    elif 'filterer' in name.lower():
                        removed.append(f"`{name}` (no longer needed)")
                    elif 'populator' in name.lower():
                        removed.append(f"`{name}` (replaced by `ProviderBasedServiceProviderBuilder`)")
                    elif name.startswith('I') and len(name) > 1 and name[1].isupper():
                        # Interface already handled in breaking changes
                        pass
                    elif 'extension' in name.lower():
                        # Extension class removals
                        removed.append(f"`{name}` (consolidated or moved)")
    
    # Scrutor changes
    if 'scrutor' in categories:
        scrutor_files = categories['scrutor']
        scrutor_modified = [f for f in scrutor_files if f.status == 'M']
        if scrutor_modified:
            changed.append("`UsingScrutorTypeRegistrar()` renamed to `UsingScrutor()`")
    
    # Examples
    if 'examples' in categories:
        example_files = categories['examples']
        example_added = [f for f in example_files if f.status == 'A']
        new_projects = detect_new_projects(example_added)
        if any('aot' in f.path.lower() for f in example_added):
            added.append("AOT/Trimming example applications (console and web)")
        if any('bundle' in f.path.lower() for f in example_added):
            added.append("Bundle auto-configuration example")
        if any('benchmark' in f.path.lower() for f in example_added):
            added.append("Performance benchmarks comparing source-gen vs reflection")
    
    # Benchmarks
    if 'benchmarks' in categories:
        bench_added = [f for f in categories['benchmarks'] if f.status == 'A']
        if bench_added:
            added.append("Performance benchmarks comparing source-gen vs reflection")
    
    # CI/CD
    if 'ci' in categories:
        ci_files = categories['ci']
        ci_modified = [f for f in ci_files if f.status in ['A', 'M']]
        if ci_modified:
            added.append("Parallel CI/CD with AOT publish validation")
    
    # Skills
    if 'skills' in categories:
        skill_added = [f for f in categories['skills'] if f.status == 'A']
        if skill_added:
            added.append("Changelog generator agent skill")
    
    # Deduplicate entries
    added = list(dict.fromkeys(added))
    changed = list(dict.fromkeys(changed))
    removed = list(dict.fromkeys(removed))
    breaking_changes = list(dict.fromkeys(breaking_changes))
    
    # Build output
    if breaking_changes:
        lines.append("### ⚠️ Breaking Changes")
        for entry in breaking_changes:
            lines.append(f"- {entry}")
        lines.append("")
    
    if added:
        lines.append("### Added")
        for entry in added:
            lines.append(f"- {entry}")
        lines.append("")
    
    if changed:
        lines.append("### Changed")
        for entry in changed:
            lines.append(f"- {entry}")
        lines.append("")
    
    if removed:
        lines.append("### Removed")
        for entry in removed:
            lines.append(f"- {entry}")
        lines.append("")
    
    # Stats comment
    stats = analysis['stats']
    lines.append(f"_({stats['total_files']} files changed, +{stats['insertions']}/-{stats['deletions']} lines)_")
    
    return '\n'.join(lines)


def main():
    parser = argparse.ArgumentParser(
        description='Generate semantic changelog from git diff analysis'
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
    
    args = parser.parse_args()
    
    # Validate refs
    if not validate_ref(args.from_ref, args.repo):
        print(f"Error: ref '{args.from_ref}' does not exist", file=sys.stderr)
        sys.exit(1)
    
    if not validate_ref(args.to_ref, args.repo):
        print(f"Error: ref '{args.to_ref}' does not exist", file=sys.stderr)
        sys.exit(1)
    
    # Get and analyze changes
    changes = get_file_changes(args.from_ref, args.to_ref, args.repo)
    
    if not changes:
        print(f"Warning: no changes found between {args.from_ref} and {args.to_ref}", 
              file=sys.stderr)
    
    analysis = analyze_changes(changes)
    
    # Generate changelog
    changelog = generate_semantic_changelog(
        analysis, changes, args.repo,
        args.from_ref, args.to_ref,
        args.version, args.date
    )
    
    # Output
    if args.output:
        args.output.write_text(changelog, encoding='utf-8')
        print(f"Wrote {args.output}", file=sys.stderr)
    else:
        print(changelog)


if __name__ == '__main__':
    main()
