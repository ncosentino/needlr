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

for vdir in "$ROOT_DIR/docs/api"/v*/; do
    if [ -d "$vdir" ]; then
        vname=$(basename "$vdir")
        echo "- [$vname]($vname/index.md)" >> "$ROOT_DIR/docs/api/index.md"
    fi
done

echo "Versioned API documentation generated successfully"
