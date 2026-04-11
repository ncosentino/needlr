#!/bin/bash
# Generate versioned API documentation for releases
# Usage: ./scripts/generate-versioned-api-docs.sh <version>
#
# This script:
# 1. Generates API docs to docs/api/v{version}/
# 2. Copies to docs/api/stable/
# 3. Updates docs/api/index.md with version links

set -e

VERSION="${1:?Usage: $0 <version>}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(dirname "$SCRIPT_DIR")"

echo "Generating API documentation for version $VERSION..."

# Generate docs to versioned directory
"$SCRIPT_DIR/generate-api-docs.sh" "$ROOT_DIR/docs/api/v$VERSION"

# Create version-specific index
cat > "$ROOT_DIR/docs/api/v$VERSION/index.md" << EOF
# API Reference v$VERSION

This documentation is for version **$VERSION**.

## Packages

EOF

for dir in "$ROOT_DIR/docs/api/v$VERSION"/NexusLabs.Needlr*/; do
    if [ -f "$dir/index.md" ]; then
        name=$(basename "$dir")
        echo "- [$name]($name/index.md)" >> "$ROOT_DIR/docs/api/v$VERSION/index.md"
    fi
done

# Copy to stable (latest release)
echo "Copying to stable..."
mkdir -p "$ROOT_DIR/docs/api/stable"
cp -r "$ROOT_DIR/docs/api/v$VERSION/"* "$ROOT_DIR/docs/api/stable/"

# Update stable index
cat > "$ROOT_DIR/docs/api/stable/index.md" << EOF
# API Reference (Stable)

This documentation is for the latest stable release: **v$VERSION**.

For development (unreleased) documentation, see [dev](../dev/index.md).

## Packages

EOF

for dir in "$ROOT_DIR/docs/api/stable"/NexusLabs.Needlr*/; do
    if [ -f "$dir/index.md" ]; then
        name=$(basename "$dir")
        echo "- [$name]($name/index.md)" >> "$ROOT_DIR/docs/api/stable/index.md"
    fi
done

# Update main API index
cat > "$ROOT_DIR/docs/api/index.md" << 'EOF'
# API Reference

Select a version:

- [Stable (Latest Release)](stable/index.md) - Recommended for most users
- [Development](dev/index.md) - Latest from main branch (may be unstable)

## All Versions

EOF

# Enumerate all released versions from git tags.
#
# Git tags are the single source of truth for "what versions have been released".
# This replaces an earlier approach that walked `docs/api/v*/` in the working
# tree, which silently lost history because .gitignore blocks per-version dirs
# from ever being committed to main. Every past release's "All Versions"
# catalog therefore only listed itself — see live site before this change for
# proof.
#
# The per-version HTML for each tag is preserved on gh-pages via peaceiris's
# keep_files: true behavior, so the trailing-slash links below resolve at
# runtime even when the source dir is not present in this checkout. Mkdocs
# strict mode tolerates these as "unrecognized relative links, left as is"
# (verified). Use trailing slash, not `/index.md`, to avoid mkdocs trying to
# resolve them against the local source tree.
if git rev-parse --git-dir > /dev/null 2>&1; then
    # --sort=-version:refname gives descending version order so the newest
    # release appears first in the catalog. Git's version sort handles
    # pre-release suffixes (0.0.2-alpha.26 > 0.0.2-alpha.9).
    for tag in $(git -C "$ROOT_DIR" tag --list 'v*' --sort=-version:refname); do
        version="${tag#v}"
        echo "- [$tag](v${version}/)" >> "$ROOT_DIR/docs/api/index.md"
    done
else
    echo "Warning: not a git repo, falling back to working-tree scan" >&2
    for vdir in "$ROOT_DIR/docs/api"/v*/; do
        if [ -d "$vdir" ]; then
            vname=$(basename "$vdir")
            echo "- [$vname]($vname/)" >> "$ROOT_DIR/docs/api/index.md"
        fi
    done
fi

echo "Versioned API documentation generated successfully"
