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

# Keep track of generated packages
GENERATED_PACKAGES=""

# Generate documentation for each XML file
for XML_FILE in $XML_FILES; do
    PROJECT_NAME=$(basename "$XML_FILE" .xml)
    
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
        
        # Check if any markdown files were generated (package has public types)
        MD_COUNT=$(find "$PROJECT_OUTPUT" -name "*.md" -type f 2>/dev/null | wc -l)
        if [ "$MD_COUNT" -gt 0 ]; then
            GENERATED_PACKAGES="$GENERATED_PACKAGES $PROJECT_NAME"
            
            # Post-process: Fix escaped angle brackets in headings
            # Replace \< and \> with HTML entities in heading lines
            find "$PROJECT_OUTPUT" -name "*.md" -type f -exec sed -i \
                -e 's/^\(##* .*\)\\</\1\&lt;/g' \
                -e 's/^\(##* .*\)\\>/\1\&gt;/g' \
                {} \;
            
            # Multiple passes to catch nested generics like IReadOnlyList\<Action\<T\>\>
            for i in 1 2 3 4; do
                find "$PROJECT_OUTPUT" -name "*.md" -type f -exec sed -i \
                    -e 's/^\(##* .*\)\\</\1\&lt;/g' \
                    -e 's/^\(##* .*\)\\>/\1\&gt;/g' \
                    {} \;
            done
        else
            echo "  Note: $PROJECT_NAME has no public types, skipping..."
            rm -rf "$PROJECT_OUTPUT"
        fi
    else
        echo "  Warning: Failed to generate docs for $PROJECT_NAME"
    fi
done

# Update index if requested
if [ "$UPDATE_INDEX" = "--update-index" ] && [ -f "$OUTPUT_DIR/index.md" ]; then
    echo "Updating index page..."
    
    # Remove old package links and placeholder text
    sed -i '/^- \[NexusLabs/d' "$OUTPUT_DIR/index.md"
    sed -i '/^\* \[NexusLabs/d' "$OUTPUT_DIR/index.md"
    sed -i '/No API documentation generated/d' "$OUTPUT_DIR/index.md"
    sed -i '/API documentation will be generated/d' "$OUTPUT_DIR/index.md"
    
    # Sort and add links to each generated package
    for PROJECT_NAME in $(echo $GENERATED_PACKAGES | tr ' ' '\n' | sort); do
        if [ -d "$OUTPUT_DIR/$PROJECT_NAME" ]; then
            echo "* [$PROJECT_NAME]($PROJECT_NAME/)" >> "$OUTPUT_DIR/index.md"
        fi
    done
fi

echo ""
echo "API documentation generated successfully at $OUTPUT_DIR"
echo "Generated packages: $(echo $GENERATED_PACKAGES | wc -w)"
