# Needlr Visual Studio Extension

This extension provides dependency injection visualization and diagnostics for Needlr-based .NET projects in Visual Studio 2022.

## Features

- **Services Tool Window**: Browse all registered services grouped by lifetime (Singleton, Scoped, Transient)
- **Go to Definition**: Double-click any service to navigate to its source code
- **Auto-refresh**: Automatically updates when the project is rebuilt

## Requirements

1. Visual Studio 2022 (17.0 or later)
2. A .NET project using Needlr for dependency injection
3. Graph export enabled in your project:

```xml
<PropertyGroup>
  <NeedlrExportGraph>true</NeedlrExportGraph>
</PropertyGroup>
```

## Installation

### From Source

1. Open the solution in Visual Studio 2022
2. Build the `NeedlrToolsExtension` project
3. Double-click the generated `.vsix` file to install

### From Marketplace

Coming soon...

## Usage

1. Build your project with the `NeedlrExportGraph` property enabled
2. Open the "Needlr Services" tool window from View > Other Windows > Needlr Services
3. Browse services grouped by lifetime
4. Double-click to navigate to source

## Development

### Prerequisites

- Visual Studio 2022 with VSIX development workload
- .NET Framework 4.8 SDK

### Building

```bash
# Open in Visual Studio and build, or:
msbuild NeedlrToolsExtension.csproj /p:Configuration=Debug
```

### Debugging

1. Set `NeedlrToolsExtension` as the startup project
2. Press F5 to launch the experimental instance
3. Open a solution with Needlr graph export enabled

## Architecture

The extension uses the [Community.VisualStudio.Toolkit](https://github.com/VsixCommunity/Community.VisualStudio.Toolkit) for simplified VSIX development.

Key components:
- `NeedlrToolsPackage`: Extension entry point and initialization
- `GraphLoader`: Watches for and loads graph files
- `NeedlrServicesToolWindow`: WPF tool window for service browsing
- `NeedlrGraph`: Data models matching the JSON schema

## License

MIT - See the main Needlr repository for details.
