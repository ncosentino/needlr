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
# Intersect git tags (source of truth for "what was released") with the
# actual v*/ directories that exist on gh-pages right now (source of truth
# for "what has live docs"). Tags for pre-gh-pages historical versions
# (<alpha.19) and tags like alpha.20 that never had a corresponding release
# deploy are silently filtered out instead of emitting broken links.
#
# The CURRENT release's version ($VERSION) is always included even though
# its directory doesn't exist on gh-pages yet — this same release will
# create it a few steps later when peaceiris deploys ./site/. Skipping it
# here would make the new release paradoxically self-excluding.
#
# Fetch uses the GitHub REST API with $GITHUB_TOKEN if available (CI path;
# release.yml has contents:write which covers the contents endpoint) and
# falls back to unauthenticated (local path; 60 req/hr rate limit is more
# than enough for one call). If the fetch fails outright we warn and fall
# back to listing ALL tags — strictly worse UX but never broken worse than
# the previous behavior.
build_gh_pages_v_dirs_list() {
    local repo_slug=""
    if [ -n "${GITHUB_REPOSITORY:-}" ]; then
        repo_slug="$GITHUB_REPOSITORY"
    elif git -C "$ROOT_DIR" remote get-url origin > /dev/null 2>&1; then
        local url
        url="$(git -C "$ROOT_DIR" remote get-url origin)"
        # Match https://github.com/OWNER/REPO(.git) or git@github.com:OWNER/REPO(.git)
        repo_slug="$(echo "$url" | sed -E 's#.*github\.com[:/]([^/]+/[^/.]+)(\.git)?$#\1#')"
    fi
    if [ -z "$repo_slug" ]; then
        return 1
    fi

    local auth_header=""
    if [ -n "${GITHUB_TOKEN:-}" ]; then
        auth_header="Authorization: token $GITHUB_TOKEN"
    fi

    local api_url="https://api.github.com/repos/${repo_slug}/contents/api?ref=gh-pages"
    local response
    if [ -n "$auth_header" ]; then
        response="$(curl -sS -H "$auth_header" -H "Accept: application/vnd.github+json" "$api_url")"
    else
        response="$(curl -sS -H "Accept: application/vnd.github+json" "$api_url")"
    fi
    if [ $? -ne 0 ] || [ -z "$response" ]; then
        return 1
    fi

    # Parse without jq (jq may not be installed locally) — grep out dir names
    # that start with "v". This is tolerant of the JSON being on one line or
    # pretty-printed.
    echo "$response" | grep -oE '"name":[[:space:]]*"v[^"]*"' | sed -E 's/.*"(v[^"]*)"$/\1/'
}

existing_v_dirs=""
filtered_tags=()
if git rev-parse --git-dir > /dev/null 2>&1; then
    existing_v_dirs="$(build_gh_pages_v_dirs_list || true)"

    if [ -n "$existing_v_dirs" ]; then
        echo "Filtering catalog against $(echo "$existing_v_dirs" | wc -l | tr -d ' ') existing gh-pages directories"
    else
        echo "Warning: could not fetch gh-pages directory list; catalog will list all git tags (may include broken links for pre-gh-pages tags)" >&2
    fi

    # --sort=-version:refname gives descending version order so the newest
    # release appears first in the catalog. Git's version sort handles
    # pre-release suffixes (0.0.2-alpha.26 > 0.0.2-alpha.9).
    for tag in $(git -C "$ROOT_DIR" tag --list 'v*' --sort=-version:refname); do
        version="${tag#v}"
        dir_name="v${version}"

        if [ -n "$existing_v_dirs" ]; then
            # Include if the directory already exists on gh-pages OR if it's
            # the version this release is about to create.
            if echo "$existing_v_dirs" | grep -qx "$dir_name" || [ "$version" = "$VERSION" ]; then
                filtered_tags+=("$tag")
            fi
        else
            # Fallback: list every tag (old behavior).
            filtered_tags+=("$tag")
        fi
    done

    for tag in "${filtered_tags[@]}"; do
        version="${tag#v}"
        echo "- [$tag](v${version}/)" >> "$ROOT_DIR/docs/api/index.md"
    done
else
    echo "Warning: not a git repo, falling back to working-tree scan" >&2
    for vdir in "$ROOT_DIR/docs/api"/v*/; do
        if [ -d "$vdir" ]; then
            vname=$(basename "$vdir")
            echo "- [$vname]($vname/)" >> "$ROOT_DIR/docs/api/index.md"
            filtered_tags+=("$vname")
        fi
    done
fi

# Emit versions.json alongside the markdown catalog. This is the data
# source for the runtime API version switcher (see
# docs/javascripts/api-version-switcher.js). mkdocs copies non-markdown
# files under docs/ into the site root, so this lands at /api/versions.json
# on the deployed site and survives across ci.yml runs via peaceiris
# keep_files:true (release.yml owns /api/versions.json the same way it
# owns /api/stable/ and /api/v*/).
#
# Emitted by hand rather than via jq (not available everywhere). The
# format is deliberately simple: a flat list of entries in nav order,
# with an optional separator. Adding an entry is one line; the JS
# renders whatever is there without schema validation.
#
# We emit commas BEFORE each entry (not after) so the trailing comma
# before `]` is never a problem even when filtered_tags is empty.
{
    printf '{\n'
    printf '  "current_stable": "%s",\n' "$VERSION"
    printf '  "entries": [\n'

    need_comma=0
    emit_entry() {
        if [ "$need_comma" -eq 1 ]; then printf ',\n'; fi
        printf '    %s' "$1"
        need_comma=1
    }

    emit_entry "$(printf '{ "label": "Stable (%s)", "path": "stable" }' "$VERSION")"
    emit_entry '{ "label": "Development (main)", "path": "dev" }'
    if [ "${#filtered_tags[@]}" -gt 0 ]; then
        emit_entry '{ "separator": true }'
        for tag in "${filtered_tags[@]}"; do
            version="${tag#v}"
            emit_entry "$(printf '{ "label": "%s", "path": "v%s" }' "$tag" "$version")"
        done
    fi

    printf '\n  ]\n'
    printf '}\n'
} > "$ROOT_DIR/docs/api/versions.json"

echo "Wrote versions.json with ${#filtered_tags[@]} version entries"

echo "Versioned API documentation generated successfully"
