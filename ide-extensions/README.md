# Needlr IDE Extensions

This directory contains IDE extensions that consume the Needlr dependency graph for visualization and diagnostics.

## Overview

Both extensions share a common architecture:

1. **Graph Loading**: Watch for and load `needlr-graph.json` or extract from `NeedlrGraph.g.cs`
2. **Service Browsing**: Tree view of services grouped by lifetime
3. **Navigation**: Go to service definitions in source code
4. **Auto-refresh**: Update when project is rebuilt

## Enabling Graph Export

Add to your `.csproj`:

```xml
<PropertyGroup>
  <NeedlrExportGraph>true</NeedlrExportGraph>
</PropertyGroup>
```

## Extensions

### VS Code Extension (`vscode/`)

TypeScript-based extension for VS Code.

```bash
cd vscode
npm install
npm run compile
# Press F5 in VS Code to debug
```

Features:
- Needlr Services tree view in Explorer
- Dependency graph webview
- Quick-pick navigation

### Visual Studio Extension (`visualstudio/`)

VSIX extension for Visual Studio 2022.

```bash
cd visualstudio/NeedlrToolsExtension
# Open in Visual Studio 2022 and build
# Or: msbuild NeedlrToolsExtension.csproj
```

Features:
- Needlr Services tool window
- Double-click navigation
- Status bar integration

## JSON Schema

The extensions consume the graph format defined in `schemas/needlr-graph-v1.schema.json`. This provides:

- IntelliSense for graph files
- Validation of graph structure
- Documentation of fields

## Development Notes

### Shared Contract

Both extensions use the same JSON schema, ensuring:
- Consistent behavior across IDEs
- Independent development
- Single source of truth for graph format

### File Discovery

Extensions look for:
1. `**/obj/**/needlr-graph.json` - Direct JSON file
2. `**/NeedlrGraph.g.cs` - Extract JSON from generated source

### Future Enhancements

- [ ] Mermaid diagram generation
- [ ] Lifetime mismatch highlighting
- [ ] Decorator chain visualization
- [ ] Interceptor pipeline view
- [ ] Search and filter
- [ ] Export to various formats
