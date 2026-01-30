# Needlr Examples

This folder contains example projects demonstrating various Needlr features and usage patterns.

## Organization

Examples are organized by the primary discovery/registration strategy:

### `Reflection/` - Reflection-Based Examples

These examples use runtime reflection for type discovery. Suitable for:
- Applications that don't require AOT/trimming
- Dynamic plugin loading scenarios
- Development/testing environments

| Example | Description |
|---------|-------------|
| `AspNetCoreApp1` | Full ASP.NET Core application with plugins |
| `ManualRegistrationExample` | Manual service registration without auto-discovery |
| `ManualRegistrationWithPluginExample` | Combines manual registration with plugin-based configuration |
| `ManualScrutorRegistrationExample` | Using Scrutor for assembly scanning |
| `MinimalWebApi` | Minimal API with reflection-based discovery |
| `PostPluginCallbackExample` | Post-plugin registration callbacks |

### `SourceGen/` - Source-Generation Examples

These examples use compile-time source generation. **Recommended for:**
- AOT-published applications
- Trimmed applications
- Production deployments where startup time matters

| Example | Description |
|---------|-------------|
| `AotSourceGenApp` | Full ASP.NET Core application with AOT/trimming enabled |
| `AotSourceGenConsoleApp` | Console application with AOT/trimming enabled |
| `AotSourceGenConsolePlugin` | Plugin assembly for the console app |
| `AotSourceGenPlugin` | Plugin assembly for the web app |
| `MinimalWebApiSourceGen` | Minimal API with source-generated discovery |

### `SignalR/` - SignalR Integration

Examples showing integration with ASP.NET Core SignalR:

| Example | Description |
|---------|-------------|
| `ChatHubExample` | Real-time chat application using Needlr's SignalR integration with source-generated hub registration |

### `SemanticKernel/` - Semantic Kernel Integration

Examples showing integration with Microsoft Semantic Kernel:

| Example | Description |
|---------|-------------|
| `SimpleSemanticKernelApp` | Basic Semantic Kernel integration with Needlr |

### `Hosting/` - Generic Host Examples

Examples showing Needlr integration with the Generic Host for worker services and console applications:

#### `Hosting/Reflection/` - Reflection-Based Hosting

| Example | Description |
|---------|-------------|
| `WorkerServiceExample` | Full worker service using Needlr's `ForHost()` with reflection - Needlr controls the host lifecycle |
| `HostBuilderIntegrationExample` | Using `UseNeedlrDiscovery()` with user-controlled `HostApplicationBuilder` and reflection |

#### `Hosting/SourceGen/` - Source-Generation Hosting

| Example | Description |
|---------|-------------|
| `WorkerServiceSourceGen` | Full worker service using Needlr's `ForHost()` with source generation - AOT compatible |
| `HostBuilderIntegrationSourceGen` | Using `UseNeedlrDiscovery()` with user-controlled `HostApplicationBuilder` and source generation |

## Running Examples

Most examples can be run with:

```bash
dotnet run --project src/Examples/<category>/<example>
```

For AOT examples, use the publish profile:

```bash
dotnet publish src/Examples/SourceGen/AotSourceGenApp -c Release
```

## Adding New Examples

When adding examples:
1. Place in the appropriate category folder
2. Add to the solution file (`NexusLabs.Needlr.slnx`)
3. Update this README
4. Follow existing naming conventions
