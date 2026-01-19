# Needlr Analyzers

Needlr includes optional Roslyn analyzers to help developers avoid common mistakes and ensure best practices.

## Core Analyzers (NexusLabs.Needlr.Analyzers)

These analyzers are included with the `NexusLabs.Needlr` package.

| Rule ID | Severity | Description |
|---------|----------|-------------|
| [NDLR0001](NDLR0001.md) | Error | Reflection API used in AOT project |
| [NDLR0002](NDLR0002.md) | Warning | Plugin has constructor dependencies |

## SignalR Analyzers (NexusLabs.Needlr.SignalR.Analyzers)

These analyzers are included with the `NexusLabs.Needlr.SignalR` package.

| Rule ID | Severity | Description |
|---------|----------|-------------|
| [NDLR1001](NDLR1001.md) | Warning | HubPath must be a constant expression |
| [NDLR1002](NDLR1002.md) | Warning | HubType must be a typeof expression |

## Suppressing Warnings

To suppress a specific analyzer warning, use pragma directives:

```csharp
#pragma warning disable NDLR0001
// Code that triggers the warning
#pragma warning restore NDLR0001
```

Or suppress in your project file for the entire project:

```xml
<PropertyGroup>
  <NoWarn>$(NoWarn);NDLR0002</NoWarn>
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
