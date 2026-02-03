#!/bin/bash
# Generate API documentation from XML doc comments
# Usage: ./scripts/generate-api-docs.sh <output-dir> [--update-index]
#
# Examples:
#   ./scripts/generate-api-docs.sh docs/api/dev --update-index
#   ./scripts/generate-api-docs.sh docs/api/v0.0.2

set -e

OUTPUT_DIR_ARG="${1:?Usage: $0 <output-dir> [--update-index]}"
UPDATE_INDEX="${2:-}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(dirname "$SCRIPT_DIR")"

# Convert output dir to absolute path
if [[ "$OUTPUT_DIR_ARG" = /* ]]; then
    OUTPUT_DIR="$OUTPUT_DIR_ARG"
else
    OUTPUT_DIR="$ROOT_DIR/$OUTPUT_DIR_ARG"
fi

echo "Generating API documentation to $OUTPUT_DIR..."

# Ensure output directory exists
mkdir -p "$OUTPUT_DIR"

# Find all XML documentation files for publishable projects
# Look directly in the project's own bin folder (not in Test project folders that reference them)
# Exclude Tests, Benchmarks, Examples, IntegrationTests, and Roslyn.Shared (internal)
declare -A SEEN_PACKAGES
XML_FILES_UNIQUE=""

while IFS= read -r xml_file; do
    pkg_name=$(basename "$xml_file" .xml)
    project_folder=$(dirname "$xml_file" | sed 's|/bin/Release/.*||' | xargs basename)
    
    # Only include if the XML is in a folder matching its package name
    # This excludes DLLs that are copied to other projects as references
    if [ "$pkg_name" = "$project_folder" ] && [ -z "${SEEN_PACKAGES[$pkg_name]:-}" ]; then
        SEEN_PACKAGES[$pkg_name]=1
        XML_FILES_UNIQUE="$XML_FILES_UNIQUE $xml_file"
    fi
done < <(find "$ROOT_DIR/src" -path "*/bin/Release/*/NexusLabs.Needlr*.xml" \
    ! -path "*/Examples/*" \
    ! -name "*Tests.xml" \
    ! -name "*Benchmarks.xml" \
    ! -name "*IntegrationTests.xml" \
    ! -name "NexusLabs.Needlr.Roslyn.Shared.xml" \
    2>/dev/null | sort)

XML_FILES="$XML_FILES_UNIQUE"

if [ -z "$XML_FILES" ]; then
    echo "No XML documentation files found. Ensure the project was built with Release configuration."
    exit 1
fi

echo "Found XML documentation files:"
for f in $XML_FILES; do echo "  - $(basename "$f")"; done

# Collect all package names for link fixing later
ALL_PACKAGES=""

# Keep track of generated packages
GENERATED_PACKAGES=""

# Generate documentation for each XML file
for XML_FILE in $XML_FILES; do
    PROJECT_NAME=$(basename "$XML_FILE" .xml)
    ALL_PACKAGES="$ALL_PACKAGES $PROJECT_NAME"
    
    # Get the corresponding DLL
    DLL_FILE="${XML_FILE%.xml}.dll"
    
    if [ ! -f "$DLL_FILE" ]; then
        echo "  Warning: DLL not found for $PROJECT_NAME, skipping..."
        continue
    fi
    
    # Create project-specific output directory
    PROJECT_OUTPUT="$OUTPUT_DIR/$PROJECT_NAME"
    mkdir -p "$PROJECT_OUTPUT"
    
    echo "Generating docs for $PROJECT_NAME..."
    
    # Run DefaultDocumentation
    if dotnet defaultdocumentation \
        --AssemblyFilePath "$DLL_FILE" \
        --DocumentationFilePath "$XML_FILE" \
        --OutputDirectoryPath "$PROJECT_OUTPUT" \
        --ConfigurationFilePath "$ROOT_DIR/defaultdocumentation.json" 2>&1; then
        
        MD_COUNT=$(find "$PROJECT_OUTPUT" -name "*.md" -type f 2>/dev/null | wc -l)
        
        # If no index.md was generated but we have markdown files, create one
        # DefaultDocumentation doesn't always create index.md for single-namespace packages
        if [ ! -f "$PROJECT_OUTPUT/index.md" ] && [ "$MD_COUNT" -gt 0 ]; then
            # Look for a namespace file matching the project name
            NAMESPACE_FILE="$PROJECT_OUTPUT/$PROJECT_NAME.md"
            if [ -f "$NAMESPACE_FILE" ]; then
                # Copy namespace file to index.md
                cp "$NAMESPACE_FILE" "$PROJECT_OUTPUT/index.md"
                echo "  Created index.md from namespace file"
            else
                # Create a simple index.md listing all files
                echo "# $PROJECT_NAME" > "$PROJECT_OUTPUT/index.md"
                echo "" >> "$PROJECT_OUTPUT/index.md"
                for md in "$PROJECT_OUTPUT"/*.md; do
                    if [ "$(basename "$md")" != "index.md" ]; then
                        name=$(basename "$md" .md)
                        echo "- [$name]($name.md)" >> "$PROJECT_OUTPUT/index.md"
                    fi
                done
                echo "  Created index.md with file listing"
            fi
        fi
        
        # Now check if we have a valid index.md
        if [ -f "$PROJECT_OUTPUT/index.md" ]; then
            GENERATED_PACKAGES="$GENERATED_PACKAGES $PROJECT_NAME"
            
            # Post-process: Fix escaped angle brackets in headings
            # Replace \< and \> with HTML entities in heading lines only
            # Use a loop to handle multiple occurrences per line (nested generics)
            find "$PROJECT_OUTPUT" -name "*.md" -type f | while read -r mdfile; do
                # Keep replacing until no more changes (handles deep nesting)
                while grep -q '^##.*\\<\|^##.*\\>' "$mdfile" 2>/dev/null; do
                    sed -i -e '/^##/s/\\</\&lt;/g' -e '/^##/s/\\>/\&gt;/g' "$mdfile"
                done
            done
        else
            echo "  Note: $PROJECT_NAME has no public types, skipping..."
            rm -rf "$PROJECT_OUTPUT"
        fi
    else
        echo "  Warning: Failed to generate docs for $PROJECT_NAME"
    fi
done

# Post-process all packages to fix internal links
# Convert Microsoft Learn URLs for Needlr types to relative links
# IMPORTANT: Process packages from longest to shortest to avoid partial matches
echo ""
echo "Post-processing: Fixing internal links..."

# Sort packages by length (longest first) using awk
SORTED_PACKAGES=$(echo $ALL_PACKAGES | tr ' ' '\n' | awk '{ print length, $0 }' | sort -rn | cut -d' ' -f2-)

for PROJECT_NAME in $GENERATED_PACKAGES; do
    PROJECT_OUTPUT="$OUTPUT_DIR/$PROJECT_NAME"
    find "$PROJECT_OUTPUT" -name "*.md" -type f | while read -r mdfile; do
        # For each known package (sorted by length, longest first), convert MS Learn links to relative paths
        for PKG in $SORTED_PACKAGES; do
            # Convert package name to lowercase for URL matching
            PKG_LOWER=$(echo "$PKG" | tr '[:upper:]' '[:lower:]')
            # Match full markdown link: (url 'tooltip') where URL contains our package
            # URL format: https://learn.microsoft.com/.../nexuslabs.needlr.xxx... 'tooltip')
            # The URL has no spaces, tooltip starts with space-quote, ends with quote-paren
            # Use extended regex (-E) for cleaner syntax
            sed -i -E "s|\(https://learn\.microsoft\.com/[^ ]*dotnet/api/${PKG_LOWER}[^ ]* '[^']*'\)|(../${PKG}/index.md)|g" "$mdfile" 2>/dev/null || true
        done
    done
done

# Update index if requested
if [ "$UPDATE_INDEX" = "--update-index" ] && [ -f "$OUTPUT_DIR/index.md" ]; then
    echo "Updating index page..."
    
    # Remove old package links and placeholder text
    sed -i '/^- \[NexusLabs/d' "$OUTPUT_DIR/index.md"
    sed -i '/^\* \[NexusLabs/d' "$OUTPUT_DIR/index.md"
    sed -i '/No API documentation generated/d' "$OUTPUT_DIR/index.md"
    sed -i '/API documentation will be generated/d' "$OUTPUT_DIR/index.md"
    
    # Sort and add links to each generated package (only if it has index.md)
    for PROJECT_NAME in $(echo $GENERATED_PACKAGES | tr ' ' '\n' | sort); do
        if [ -f "$OUTPUT_DIR/$PROJECT_NAME/index.md" ]; then
            echo "* [$PROJECT_NAME]($PROJECT_NAME/index.md)" >> "$OUTPUT_DIR/index.md"
        fi
    done
fi

echo ""
echo "API documentation generated successfully at $OUTPUT_DIR"
echo "Generated packages: $(echo $GENERATED_PACKAGES | wc -w)"
