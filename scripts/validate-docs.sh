#!/bin/bash
# Validate that the documentation can be built cleanly.
# Run this before pushing to catch doc build failures early.
#
# Usage:
#   ./scripts/validate-docs.sh           # full validation
#   ./scripts/validate-docs.sh --quick   # link checks only, no mkdocs build

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(dirname "$SCRIPT_DIR")"
QUICK="${1:-}"

ERRORS=0

check_index_links() {
    local INDEX_FILE="$1"
    local BASE_DIR
    BASE_DIR="$(dirname "$INDEX_FILE")"

    if [ ! -f "$INDEX_FILE" ]; then
        echo "  SKIP: $INDEX_FILE does not exist"
        return
    fi

    local BROKEN=0
    while IFS= read -r line; do
        # Extract markdown link targets: [...](target)
        while [[ "$line" =~ \]\(([^)]+)\) ]]; do
            TARGET="${BASH_REMATCH[1]}"
            line="${line#*"${BASH_REMATCH[0]}"}"

            # Skip external links and anchors
            [[ "$TARGET" == http* ]] && continue
            [[ "$TARGET" == \#* ]] && continue

            RESOLVED="$BASE_DIR/$TARGET"
            if [ ! -f "$RESOLVED" ] && [ ! -d "$RESOLVED" ]; then
                echo "  BROKEN: $TARGET  (in $INDEX_FILE)"
                BROKEN=$((BROKEN + 1))
            fi
        done
    done < "$INDEX_FILE"

    if [ "$BROKEN" -gt 0 ]; then
        ERRORS=$((ERRORS + BROKEN))
        echo "  -> $BROKEN broken link(s) found"
    fi
}

echo "=== Needlr Docs Validation ==="
echo ""

echo "--- Checking docs/api/stable/index.md links ---"
STABLE_PKG_COUNT=$(find "$ROOT_DIR/docs/api/stable" -name "index.md" -mindepth 2 -maxdepth 2 2>/dev/null | wc -l || echo 0)
echo "Stable package directories present: $STABLE_PKG_COUNT"
if [ "$STABLE_PKG_COUNT" -eq 0 ]; then
    echo "  WARNING: No stable package subdirectories found."
    echo "  This means the last release did not commit stable docs (known release.yml bug)."
    echo "  CI will use a placeholder; run a release to regenerate stable docs properly."
    echo "  Skipping link check for stable/index.md."
else
    check_index_links "$ROOT_DIR/docs/api/stable/index.md"
fi
echo ""

echo "--- Checking docs/api/dev/index.md links ---"
DEV_PKG_COUNT=$(find "$ROOT_DIR/docs/api/dev" -name "index.md" -mindepth 2 -maxdepth 2 2>/dev/null | wc -l || echo 0)
echo "Dev package directories present: $DEV_PKG_COUNT"
if [ "$DEV_PKG_COUNT" -eq 0 ]; then
    echo "  WARNING: No dev package subdirectories found."
    echo "  Run: dotnet build src/NexusLabs.Needlr.slnx -c Release"
    echo "       ./scripts/generate-api-docs.sh docs/api/dev --update-index"
else
    check_index_links "$ROOT_DIR/docs/api/dev/index.md"
fi
echo ""

if [ "$QUICK" = "--quick" ]; then
    echo "=== Quick check complete (skipped mkdocs build) ==="
    if [ "$ERRORS" -gt 0 ]; then
        echo "FAIL: $ERRORS broken link(s) found."
        exit 1
    fi
    echo "PASS: No broken links."
    exit 0
fi

echo "--- Running mkdocs build --strict ---"
cd "$ROOT_DIR"
if python -m mkdocs build --strict 2>&1; then
    echo "mkdocs build: PASS"
else
    echo "mkdocs build: FAIL"
    ERRORS=$((ERRORS + 1))
fi
echo ""

echo "=== Validation complete ==="
if [ "$ERRORS" -gt 0 ]; then
    echo "FAIL: $ERRORS error(s) found. Fix them before pushing."
    exit 1
fi
echo "PASS: Docs are valid."
