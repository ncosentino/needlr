# CHANGELOG.md Format Specification

This document defines the exact format for CHANGELOG.md files to ensure reliable parsing by CI/CD workflows.

## File Structure

```markdown
# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [X.Y.Z] - YYYY-MM-DD

### Category
- Entry

## [Previous] - YYYY-MM-DD
...
```

## Version Header Format

```
## [VERSION] - DATE
```

### Rules
- Exactly two `#` characters followed by space
- Version in square brackets `[...]`
- Space, hyphen, space ` - `
- Date in ISO format `YYYY-MM-DD`

### Regex Pattern
```regex
^## \[([^\]]+)\] - (\d{4}-\d{2}-\d{2})$
```

### Valid Examples
```markdown
## [1.0.0] - 2026-01-19
## [0.0.2-alpha.1] - 2026-01-19
## [2.0.0-rc.1] - 2026-01-19
```

### Invalid Examples
```markdown
## [1.0.0] 2026-01-19      # Missing hyphen separator
## [1.0.0] - 01-19-2026    # Wrong date format
##[1.0.0] - 2026-01-19     # Missing space after ##
## v1.0.0 - 2026-01-19     # Version not in brackets
```

## Category Headers

Categories use `###` headers within a version section.

### Standard Categories (in order)
```markdown
### ⚠️ Breaking Changes
### Added
### Changed
### Deprecated
### Removed
### Fixed
### Security
```

### Rules
- Exactly three `#` characters followed by space
- Category name exactly as specified (case-sensitive)
- Breaking Changes includes the ⚠️ emoji
- Empty categories should be omitted

### Regex Pattern
```regex
^### (.+)$
```

## Entry Format

Entries are unordered list items under category headers.

### Rules
- Start with `- ` (hyphen, space)
- Single line preferred
- Code references use backticks: `ClassName`
- No trailing punctuation unless sentence

### Valid Examples
```markdown
- Added `IInjectableTypeProvider` interface
- Removed support for .NET 6
- Fixed null reference in plugin discovery
```

### Multi-line Entries
For complex entries, use indentation:
```markdown
- Refactored service registration
  - Now uses provider pattern
  - Assemblies configured at construction
```

## Section Boundaries

A version section:
- **Starts**: At `## [VERSION]` header
- **Ends**: At next `## [` or end of file

This enables reliable extraction:
```bash
# Extract section for specific version
awk -v ver="1.0.0" '
  /^## \[/ { 
    if (found) exit
    if (index($0, "[" ver "]")) found=1
  }
  found { print }
' CHANGELOG.md
```

## Special Sections

### Unreleased
Optional section for accumulating changes before release:
```markdown
## [Unreleased]

### Added
- Work in progress feature
```

When releasing, entries move from `[Unreleased]` to the new version section.

### Comparison Links (Optional)
At the bottom of the file:
```markdown
[Unreleased]: https://github.com/org/repo/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/org/repo/compare/v0.9.0...v1.0.0
[0.9.0]: https://github.com/org/repo/releases/tag/v0.9.0
```

## Complete Example

```markdown
# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.1.0] - 2026-02-01

### Added
- New `IPluginTypeProvider` interface
- AOT Console Example application

### Changed
- Source generation is now the default pattern

### Fixed
- Plugin discovery in trimmed applications

## [1.0.0] - 2026-01-15

### ⚠️ Breaking Changes
- Removed `ITypeRegistrar` interface
- Renamed `UsingScrutorTypeRegistrar()` to `UsingScrutor()`

### Added
- Initial stable release
- Source generation support
- Reflection fallback

[Unreleased]: https://github.com/org/repo/compare/v1.1.0...HEAD
[1.1.0]: https://github.com/org/repo/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/org/repo/releases/tag/v1.0.0
```

## Validation

To validate a CHANGELOG.md file:

```python
import re

VERSION_PATTERN = re.compile(r'^## \[([^\]]+)\] - (\d{4}-\d{2}-\d{2})$')
CATEGORY_PATTERN = re.compile(r'^### (.+)$')

VALID_CATEGORIES = {
    '⚠️ Breaking Changes',
    'Added',
    'Changed', 
    'Deprecated',
    'Removed',
    'Fixed',
    'Security'
}

def validate_changelog(content: str) -> list[str]:
    errors = []
    for i, line in enumerate(content.splitlines(), 1):
        if line.startswith('## ['):
            if not VERSION_PATTERN.match(line):
                errors.append(f"Line {i}: Invalid version header format")
        elif line.startswith('### '):
            match = CATEGORY_PATTERN.match(line)
            if match and match.group(1) not in VALID_CATEGORIES:
                errors.append(f"Line {i}: Unknown category '{match.group(1)}'")
    return errors
```
