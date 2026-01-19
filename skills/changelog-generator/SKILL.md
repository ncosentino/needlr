---
name: changelog-generator
description: >
  Analyzes git history between two refs to generate changelog entries.
  Understands conventional commits, computes net delta to avoid redundant entries,
  and produces Keep a Changelog formatted output. Use when preparing releases,
  documenting breaking changes, or summarizing development work between versions.
license: MIT
compatibility: Requires git and Python 3.10+. Works with any git repository.
metadata:
  author: nexus-labs
  version: "1.0"
allowed-tools: Bash(git:*) Bash(python:*) Read Write
---

# Changelog Generator

Generates well-structured changelog entries by analyzing git history between two refs.

## When to Use

- Preparing a release and need to document changes
- Summarizing work done between versions
- Identifying breaking changes before release
- Creating release notes for GitHub/NuGet

## Quick Start

```bash
# Generate changelog section for a new version
python skills/changelog-generator/scripts/generate.py \
  --from v0.0.1 \
  --to HEAD \
  --version 0.0.2

# Prepend to existing CHANGELOG.md
python skills/changelog-generator/scripts/generate.py \
  --from v0.0.1-alpha.19 \
  --to HEAD \
  --version 0.0.2-alpha.1 \
  --mode full \
  --output CHANGELOG.md
```

## Parameters

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `--from` | Yes | - | Starting ref (exclusive) - tag, commit, or branch |
| `--to` | No | `HEAD` | Ending ref (inclusive) |
| `--version` | Yes | - | Version string for the header (e.g., "1.2.0") |
| `--date` | No | Today | Release date in YYYY-MM-DD format |
| `--repo` | No | `.` | Path to git repository |
| `--output` | No | stdout | Output file path |
| `--mode` | No | `section` | `section` (just this version) or `full` (prepend to existing) |

## Output Format

The tool generates [Keep a Changelog](https://keepachangelog.com/) formatted markdown:

```markdown
## [1.2.0] - 2026-01-19

### ⚠️ Breaking Changes
- Removed `ITypeRegistrar` interface

### Added
- New `IInjectableTypeProvider` interface

### Changed
- Source generation is now the default

### Fixed
- Plugin discovery in AOT scenarios
```

See [FORMAT.md](references/FORMAT.md) for the complete format specification.

## How It Works

1. **Extracts commit history** between the two refs
2. **Parses conventional commits** (feat:, fix:, etc.) when present
3. **Computes net delta** to identify actual changes (ignoring churn)
4. **Detects breaking changes** from commit messages and API removals
5. **Categorizes changes** into standard Keep a Changelog sections
6. **Deduplicates** related commits into single entries
7. **Formats output** as parseable markdown

## Conventional Commits Mapping

| Commit Type | Changelog Category |
|-------------|-------------------|
| `feat:` | Added |
| `fix:` | Fixed |
| `refactor:` | Changed |
| `perf:` | Changed |
| `docs:` | (excluded by default) |
| `test:` | (excluded by default) |
| `chore:` | (excluded by default) |
| `BREAKING CHANGE:` | ⚠️ Breaking Changes |

## Examples

### Generate Section Only
```bash
python generate.py --from v1.0.0 --to v1.1.0 --version 1.1.0
```

Output goes to stdout, can be copied into CHANGELOG.md manually.

### Update CHANGELOG.md Directly
```bash
python generate.py \
  --from v1.0.0 \
  --to HEAD \
  --version 1.1.0 \
  --mode full \
  --output CHANGELOG.md
```

Inserts new section after the header, before previous versions.

### Specify Custom Date
```bash
python generate.py \
  --from v1.0.0 \
  --to v1.1.0 \
  --version 1.1.0 \
  --date 2026-02-01
```

## Edge Cases

- **Merge commits**: Analyzed for their net effect, not individual commits
- **Reverts**: Paired with original commit and excluded if they cancel out
- **Empty range**: Returns empty section with comment
- **Non-conventional commits**: Categorized by file patterns and keywords
- **Missing refs**: Exits with error and helpful message

## Integration with CI

The release workflow extracts the version section from CHANGELOG.md:

```yaml
- name: Extract Release Notes
  run: |
    VERSION="${GITHUB_REF_NAME#v}"
    awk -v ver="$VERSION" '
      /^## \[/ { if (found) exit; if (index($0, "[" ver "]")) found=1 }
      found { print }
    ' CHANGELOG.md
```

This requires CHANGELOG.md to follow the format specification exactly.
