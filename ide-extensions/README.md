# Needlr IDE Extensions

This directory contains IDE extensions that consume the Needlr dependency graph for visualization and diagnostics.

## Overview

Both extensions share a common architecture:

1. **Graph Loading**: Watch for and load `needlr-graph.json` or extract from `NeedlrGraph.g.cs`
2. **Service Browsing**: Tree view of services grouped by lifetime
3. **Navigation**: Go to service definitions in source code
4. **Auto-refresh**: Update when project is rebuilt

## Enabling Graph Export

Add the property to a solution-level `Directory.Build.props` so each project owns and
emits its source locations:

```xml
<Project>
  <PropertyGroup>
    <NeedlrExportGraph>true</NeedlrExportGraph>
  </PropertyGroup>
</Project>
```

The extensions merge every project graph by fully qualified type name, prefer the
producer entry with a source location, and backfill interface locations. A consuming
project's graph may contain referenced services with null locations when Roslyn only has
PE metadata; the referenced project's own graph supplies navigation within the
workspace.

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

Each project graph is authoritative for source locations in its own assembly. External
NuGet package locations remain unavailable until a SourceLink-aware navigation design is
introduced.

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
