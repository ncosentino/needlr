# Needlr VS Code Extension

This extension provides dependency injection visualization and diagnostics for Needlr-based .NET projects.

## Features

- **Services Tree View**: Browse all registered services grouped by lifetime (Singleton, Scoped, Transient)
- **Dependency Graph Visualization**: See the full dependency graph with all services and their relationships
- **Go to Definition**: Click on any service to navigate to its source code
- **Auto-refresh**: Automatically updates when the project is rebuilt

## Requirements

1. A .NET project using Needlr for dependency injection
2. Graph export enabled in your project:

```xml
<PropertyGroup>
  <NeedlrExportGraph>true</NeedlrExportGraph>
</PropertyGroup>
```

## Usage

1. Build your project with the `NeedlrExportGraph` property enabled
2. The extension will automatically detect the generated graph
3. Use the "Needlr Services" view in the Explorer sidebar
4. Or run "Needlr: Show Dependency Graph" from the command palette

## Commands

- `Needlr: Show Dependency Graph` - Opens a webview with the full dependency graph
- `Needlr: Refresh Dependency Graph` - Manually refreshes the graph from disk
- `Needlr: Go to Service` - Quick-pick to navigate to any service

## Configuration

- `needlr.graphFilePath`: Glob pattern to find the Needlr graph file (default: `**/obj/**/needlr-graph.json`)
- `needlr.autoRefresh`: Automatically refresh the graph when the file changes (default: `true`)

## Development

```bash
# Install dependencies
npm install

# Compile
npm run compile

# Watch mode
npm run watch

# Package extension
npx vsce package
```

## License

MIT - See the main Needlr repository for details.
