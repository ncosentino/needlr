---
description: Register plugin types emitted by a second source generator with Needlr using RegisterPlugins() and a module initializer, bypassing the Roslyn generator isolation boundary.
---

# Cross-Generator Plugin Registration

Needlr's `TypeRegistryGenerator` discovers plugin types at compile time by inspecting the compilation. This works perfectly for hand-written types, but breaks down in one specific scenario: when a **second source generator** in the same assembly emits plugin types at compile time.

## The Roslyn Isolation Constraint

Roslyn incremental generators all receive the same **original compilation snapshot**. No generator can see types emitted by another generator in the same build. This is by design and will not change.

The consequence: if your second generator emits a type (e.g. a configuration record, a cache provider, a generated strategy class), `TypeRegistryGenerator` has no knowledge of it and cannot include it in the `TypeRegistry`. At runtime, `IPluginFactory.CreatePluginsFromAssemblies<T>()` returns nothing for those types — even though the types exist in the assembly.

```
Build timeline:
  Original compilation ──┬──▶ TypeRegistryGenerator  ──▶ NeedlrTypeRegistrations.g.cs
                         │                                 (does NOT include MyGeneratedType)
                         └──▶ YourSecondGenerator    ──▶ MyGeneratedType.g.cs
```

## The Solution: `RegisterPlugins()`

`NeedlrSourceGenBootstrap.RegisterPlugins()` lets your second generator emit a `[ModuleInitializer]` that registers its types with Needlr **at runtime**, before any application code runs.

```csharp
[ModuleInitializer]
internal static void Initialize()
{
    NeedlrSourceGenBootstrap.RegisterPlugins(static () =>
    [
        new PluginTypeInfo(
            typeof(MyCacheConfiguration),
            [typeof(ICacheConfiguration)],
            static () => new MyCacheConfiguration(),
            [])
    ]);
}
```

The CLR guarantees all `[ModuleInitializer]` methods in a loaded assembly complete before any user code in that assembly executes. By the time the application calls `IPluginFactory.CreatePluginsFromAssemblies<T>()`, all providers — from both `TypeRegistryGenerator` and your second generator — are already combined.

## Why Ordering Between Generators Doesn't Matter

`TypeRegistryGenerator` and your second generator each write to `NeedlrSourceGenBootstrap._registrations` (an append-only list) via separate module initializers. The reader (`TryGetProviders`) is only ever called by application code, which runs **after** all module initializers have completed.

Because the readers and writers never overlap in time, the order in which the two module initializers fire is irrelevant.

## Deduplication

`NeedlrSourceGenBootstrap.Combine()` deduplicates by `PluginType` across all registered providers. If a type is hand-written (visible to `TypeRegistryGenerator`) **and** also registered via `RegisterPlugins()`, exactly one instance appears in the merged provider. This is safe to rely on.

## Implementing It in Your Generator

In your second generator, emit a static non-generic class containing a `[ModuleInitializer]` method:

```csharp
// --- emitted by YourSecondGenerator ---
using System.Runtime.CompilerServices;
using NexusLabs.Needlr.Generators;

#pragma warning disable CA2255  // [ModuleInitializer] is valid in advanced source-generator scenarios
internal static class YourGeneratedTypeRegistrations
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        NeedlrSourceGenBootstrap.RegisterPlugins(static () =>
        [
            new PluginTypeInfo(
                typeof(FooCacheConfiguration),
                [typeof(ICacheConfiguration)],
                static () => new FooCacheConfiguration(),
                []),
            new PluginTypeInfo(
                typeof(BarCacheConfiguration),
                [typeof(ICacheConfiguration)],
                static () => new BarCacheConfiguration(),
                [])
        ]);
    }
}
```

The `CA2255` suppression is appropriate here — `[ModuleInitializer]` is exactly what this diagnostic calls "advanced source generator scenarios".

## Simulating the Pattern Without a Real Second Generator

To test the pattern end-to-end, create a project with `NeedlrAutoGenerate=false`. This prevents `TypeRegistryGenerator` from emitting any `Register()` call for that assembly, so any types there can **only** reach the Needlr registry through `RegisterPlugins()`.

```xml
<!-- MyGeneratedTypes.csproj -->
<PropertyGroup>
  <NeedlrAutoGenerate>false</NeedlrAutoGenerate>
</PropertyGroup>
```

```csharp
// The module initializer that a real generator would emit
internal static class GeneratedTypeRegistrations
{
#pragma warning disable CA2255
    [ModuleInitializer]
    internal static void Initialize()
#pragma warning restore CA2255
    {
        NeedlrSourceGenBootstrap.RegisterPlugins(static () =>
        [
            new PluginTypeInfo(
                typeof(MyGeneratedPlugin),
                [typeof(IMyPlugin)],
                static () => new MyGeneratedPlugin(),
                [])
        ]);
    }
}
```

An integration test referencing this project verifies that `MyGeneratedPlugin` is discoverable at runtime. If `RegisterPlugins()` is ever broken, the test fails — whereas a test using a hand-written type (visible to `TypeRegistryGenerator`) would pass even with `RegisterPlugins()` as a no-op.

The `MultiProjectApp.Features.CrossGenSimulation` project in this repository demonstrates this pattern. See `src/Examples/MultiProjectApp/` for the full structure.

## What Not To Do

**Don't call `RegisterPlugins()` from application startup code.** Module initializers run automatically when an assembly is loaded — you should never need to call this from `Program.cs` or a plugin's `Configure()` method. If you find yourself doing that, something has gone wrong in your generator's code emission.

**Don't call `Register()` instead of `RegisterPlugins()`.** `Register()` expects both an injectable-type provider and a plugin-type provider. `RegisterPlugins()` is the purpose-built overload that takes only plugin types and fills in an empty injectable-type provider automatically.
