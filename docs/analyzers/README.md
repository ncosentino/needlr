# Needlr Analyzers

Needlr includes optional Roslyn analyzers to help developers avoid common mistakes and ensure best practices.

## Core Analyzers (NexusLabs.Needlr.Analyzers)

These analyzers are included with the `NexusLabs.Needlr` package.

| Rule ID | Severity | Description |
|---------|----------|-------------|
| [NDLRCOR001](NDLRCOR001.md) | Error | Reflection API used in AOT project |
| [NDLRCOR002](NDLRCOR002.md) | Warning | Plugin has constructor dependencies |

## SignalR Analyzers (NexusLabs.Needlr.SignalR.Analyzers)

These analyzers are included with the `NexusLabs.Needlr.SignalR` package.

| Rule ID | Severity | Description |
|---------|----------|-------------|
| [NDLRSIG001](NDLRSIG001.md) | Warning | HubPath must be a constant expression |
| [NDLRSIG002](NDLRSIG002.md) | Warning | HubType must be a typeof expression |

## Generator Diagnostics (NexusLabs.Needlr.Generators)

These diagnostics are emitted by the source generator to detect configuration issues at compile time.

| Rule ID | Severity | Description |
|---------|----------|-------------|
| [NDLRGEN001](NDLRGEN001.md) | Error | Internal type in referenced assembly cannot be registered |
| [NDLRGEN002](NDLRGEN002.md) | Error | Referenced assembly has internal plugin types but no type registry |

## Diagnostic ID Naming Convention

Needlr uses a component-based naming convention for diagnostic IDs:

| Component | Prefix | Example |
|-----------|--------|---------|
| Core Analyzers | `NDLRCOR` | `NDLRCOR001` |
| SignalR Analyzers | `NDLRSIG` | `NDLRSIG001` |
| Source Generators | `NDLRGEN` | `NDLRGEN001` |

## Suppressing Warnings

To suppress a specific analyzer warning, use pragma directives:

```csharp
#pragma warning disable NDLRCOR001
// Code that triggers the warning
#pragma warning restore NDLRCOR001
```

Or suppress in your project file for the entire project:

```xml
<PropertyGroup>
  <NoWarn>$(NoWarn);NDLRCOR002</NoWarn>
</PropertyGroup>
```

## Configuration

Analyzers are automatically enabled when you reference the Needlr packages. No additional configuration is required.

For AOT projects, ensure your project has the appropriate settings for the analyzers to detect AOT mode:

```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
  <!-- or -->
  <IsAotCompatible>true</IsAotCompatible>
</PropertyGroup>
```
