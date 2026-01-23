# Transitive Dependency Demo

This example demonstrates Needlr's **automatic assembly force-loading** feature for source generation.

## The Problem

When using source generation with multi-project solutions, you may encounter a subtle but critical issue:

1. Your host application references `FeatureA` and `FeatureB` assemblies
2. Your code only directly uses types from `FeatureB`
3. `FeatureA` has plugins that register important services
4. `FeatureB` depends on those services

**Without force-loading:** The CLR never loads `FeatureA.dll` because no code references its types. This means:
- `FeatureA`'s module initializer never runs
- `FeatureA`'s plugins are never discovered
- Services registered by `FeatureA` are missing
- `FeatureB` fails when it tries to use those services

## The Solution

Needlr's source generator automatically:

1. **Discovers** all referenced assemblies with `[GenerateTypeRegistry]`
2. **Generates** `typeof()` calls to force those assemblies to load
3. **Loads** assemblies in alphabetical order by default

## Controlling Assembly Order

Assembly ordering is configured at the Syringe level using the same expression-based API for both reflection and source-gen:

```csharp
new Syringe()
    .UsingSourceGen()
    .UsingAssemblyProvider(builder => builder
        .OrderAssemblies(order => order
            .By(a => a.Name.StartsWith("TransitiveDemo.FeatureA")))  // FeatureA first
        .Build())
    .ForWebApplication()
    .BuildWebApplication();
```

Or use the built-in preset:

```csharp
.UseLibTestEntryOrdering()  // Libraries → Executables → Tests
```

## Project Structure

```
TransitiveDependencyDemo/
├── TransitiveDemo.Host/          # Main application (only uses FeatureB types!)
├── TransitiveDemo.FeatureA/      # Registers ICoreLogger (never used directly in Host)
└── TransitiveDemo.FeatureB/      # Uses ICoreLogger, provides IFeatureBService
```

## Running the Demo

```bash
cd src/Examples/SourceGen/TransitiveDependencyDemo/TransitiveDemo.Host
dotnet run
```

Expected output:
```
=== TransitiveDemo: Automatic Assembly Force-Loading ===

This demo shows that plugins from transitive dependencies are discovered
even when their types are never directly referenced in code.

[FeatureA] Plugin executing - registering ICoreLogger
[FeatureB] Post-build plugin executing
[CoreLogger] FeatureB plugin initialized successfully!

=== Service Resolution ===
[CoreLogger] FeatureB is doing work!

=== Success! ===
All plugins executed successfully, including FeatureA's plugin
which we never directly referenced in code!
```

## How It Works

After building, check `TransitiveDemo.Host/obj/Generated/` for the generated bootstrap code:

```csharp
// In NeedlrSourceGenBootstrap.g.cs
[MethodImpl(MethodImplOptions.NoInlining)]
private static void ForceLoadReferencedAssemblies()
{
    _ = typeof(global::TransitiveDemo.FeatureA.Generated.TypeRegistry).Assembly;
    _ = typeof(global::TransitiveDemo.FeatureB.Generated.TypeRegistry).Assembly;
}
```

This ensures both assemblies load before any plugins are executed.

## AOT Compatibility

This feature is fully AOT-compatible because:
- `typeof()` is resolved at compile time
- No reflection is used
- The assembly references are statically known
