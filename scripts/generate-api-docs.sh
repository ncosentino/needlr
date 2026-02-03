#!/bin/bash
# Generate API documentation from XML doc comments
# Usage: ./scripts/generate-api-docs.sh [output-dir]

set -e

OUTPUT_DIR="${1:-docs/api/dev}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(dirname "$SCRIPT_DIR")"

echo "Generating API documentation to $OUTPUT_DIR..."

# Ensure output directory exists
mkdir -p "$OUTPUT_DIR"

# Build the solution first to ensure XML files are generated
echo "Building solution..."
dotnet build "$ROOT_DIR/src/NexusLabs.Needlr.slnx" -c Release --no-restore -v quiet

# Find all XML documentation files for publishable projects (exclude Tests, Benchmarks, Examples)
XML_FILES=$(find "$ROOT_DIR/src" -path "*/bin/Release/*/NexusLabs.Needlr*.xml" \
    ! -name "*Tests.xml" \
    ! -name "*Benchmarks.xml" \
    ! -name "*IntegrationTests.xml" \
    | sort)

if [ -z "$XML_FILES" ]; then
    echo "No XML documentation files found. Ensure projects have <GenerateDocumentationFile>true</GenerateDocumentationFile>"
    exit 1
fi

echo "Found XML documentation files:"
echo "$XML_FILES" | while read -r f; do echo "  - $(basename "$f")"; done

# Generate documentation for each XML file
for XML_FILE in $XML_FILES; do
    PROJECT_NAME=$(basename "$XML_FILE" .xml)
    echo "Generating docs for $PROJECT_NAME..."
    
    # Get the corresponding DLL
    DLL_FILE="${XML_FILE%.xml}.dll"
    
    if [ ! -f "$DLL_FILE" ]; then
        echo "  Warning: DLL not found for $PROJECT_NAME, skipping..."
        continue
    fi
    
    # Create project-specific output directory
    PROJECT_OUTPUT="$OUTPUT_DIR/$PROJECT_NAME"
    mkdir -p "$PROJECT_OUTPUT"
    
    # Run DefaultDocumentation
    dotnet defaultdocumentation \
        --AssemblyFilePath "$DLL_FILE" \
        --DocumentationFilePath "$XML_FILE" \
        --OutputDirectoryPath "$PROJECT_OUTPUT" \
        --ConfigurationFilePath "$ROOT_DIR/defaultdocumentation.json" \
        || echo "  Warning: Failed to generate docs for $PROJECT_NAME"
done

# Create index page
echo "Creating index page..."
cat > "$OUTPUT_DIR/index.md" << 'EOF'
# API Reference

This documentation is auto-generated from XML doc comments in the source code.

## Packages

EOF

# Add links to each package
for XML_FILE in $XML_FILES; do
    PROJECT_NAME=$(basename "$XML_FILE" .xml)
    if [ -d "$OUTPUT_DIR/$PROJECT_NAME" ]; then
        echo "- [$PROJECT_NAME]($PROJECT_NAME/index.md)" >> "$OUTPUT_DIR/index.md"
    fi
done

echo ""
echo "API documentation generated successfully at $OUTPUT_DIR"
