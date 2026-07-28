---
description: Register ASP.NET Core SignalR services with Needlr and map hubs through either source-generated AOT-safe endpoints or an explicit reflection fallback.
---

# SignalR

`NexusLabs.Needlr.SignalR` registers SignalR services during Needlr web
application construction and supports two explicit hub-mapping paths:

- source-generated mapping for trimming and Native AOT;
- reflection mapping for applications that intentionally choose runtime discovery.

Hub endpoints are never mapped automatically. Call exactly one mapping extension after
building the application.

## Quick Start

Reference the SignalR integration:

```xml
<PackageReference Include="NexusLabs.Needlr.SignalR" />
```

Define a hub and its compile-time registration:

```csharp
using Microsoft.AspNetCore.SignalR;

using NexusLabs.Needlr.SignalR;

public sealed class ChatHub : Hub
{
}

public sealed class ChatHubRegistration : IHubRegistrationPlugin
{
    public string HubPath => "/chat";

    public Type HubType => typeof(ChatHub);
}
```

Build with source-generated Needlr composition and call the generated extension from
your assembly's `Generated` namespace:

```csharp
using MyApplication.Generated;

using NexusLabs.Needlr.AspNet;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;

var app = new Syringe()
    .UsingSourceGen()
    .BuildWebApplication();

app.MapGeneratedHubs();
await app.RunAsync();
```

The generated method emits direct `MapHub<THub>()` calls. It performs no runtime
reflection and is compatible with trimming and Native AOT.

## Explicit Reflection Mapping

Applications that intentionally use reflection can map discovered
`IHubRegistrationPlugin` implementations after building the application:

```csharp
using NexusLabs.Needlr.SignalR;

app.UseSignalRHubsWithReflection();
```

`UseSignalRHubsWithReflection()` is annotated with
`RequiresUnreferencedCodeAttribute` and `RequiresDynamicCodeAttribute`. Do not use it
in trimmed or Native AOT applications.

The reflection mapper is not a Needlr web-application plugin and is never added to
automatic plugin discovery. This keeps `UsingSourceGen()` reflection-free and prevents
calling `MapGeneratedHubs()` from mapping the same hub a second time.

## Mapping Contract

- `SignalRWebApplicationBuilderPlugin` automatically registers SignalR services.
- `IHubRegistrationPlugin` supplies the hub type and route metadata.
- `MapGeneratedHubs()` maps compile-time registrations explicitly.
- `UseSignalRHubsWithReflection()` maps runtime-discovered registrations explicitly.
- Do not call both mapping extensions for the same application.

`[DoNotAutoRegister]` controls DI service registration, not plugin discovery. A type
that implements a Needlr plugin interface is eligible to execute automatically, so an
opt-in fallback must not implement `IWebApplicationPlugin`.

## Analyzers

| Diagnostic | Severity | Description |
|------------|----------|-------------|
| [NDLRSIG001](analyzers/NDLRSIG001.md) | Warning | HubPath must be a constant expression |
| [NDLRSIG002](analyzers/NDLRSIG002.md) | Warning | HubType must be a `typeof` expression |
| [NDLRSIG003](analyzers/NDLRSIG003.md) | Error | Hub registration plugins cannot use generated-constructor generation |

## See Also

- [Plugin Development](plugin-development.md)
- [Cross-Generator Plugin Registration](cross-generator-plugins.md)
- [Source-Generated SignalR Example](https://github.com/ncosentino/needlr/tree/main/src/Examples/SignalR/ChatHubExample)
