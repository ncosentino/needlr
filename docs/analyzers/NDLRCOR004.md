# NDLRCOR004: Injectable type in global namespace may not be discovered

## Cause

A type that appears to be designed for dependency injection is defined in the **global namespace** (no `namespace` declaration), but the assembly's `[GenerateTypeRegistry]` attribute has `IncludeNamespacePrefixes` set to values that don't include an empty string (`""`).

## Rule Description

When using Needlr's source generation with namespace filtering, types in the global namespace are excluded by default. This is because:

1. **Namespace filtering is exact prefix matching**: If you set `IncludeNamespacePrefixes = new[] { "MyCompany" }`, only types in namespaces starting with `MyCompany.` are included.

2. **Global namespace has no prefix**: Types without a `namespace` declaration have no namespace to match against.

3. **Silent exclusion**: Unlike a compile error, these types are silently skipped during type discovery.

This analyzer detects types that:
- Are in the global namespace
- Appear to be injectable (have DI attributes, implement interfaces, or have constructor dependencies)
- Would be excluded by the current `IncludeNamespacePrefixes` configuration

```csharp
// ⚠️ This triggers the warning
[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "MyCompany" })]

public class AmplitudeConfiguration  // No namespace! Won't be discovered!
{
    public string ApiKey { get; set; }
}
```

## How to Fix

### Option 1: Add a Namespace (Recommended)

Move the type into an appropriate namespace:

```csharp
namespace MyCompany.Telemetry
{
    public class AmplitudeConfiguration
    {
        public string ApiKey { get; set; }
    }
}
```

### Option 2: Include Global Namespace in Prefixes

Add an empty string `""` to your `IncludeNamespacePrefixes` to explicitly include global namespace types:

```csharp
[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "MyCompany", "" })]
```

This tells Needlr: "Include types in `MyCompany.*` namespaces AND types in the global namespace."

### Option 3: Mark as Not Injectable

If the type shouldn't be registered with DI, add `[DoNotInject]` or `[DoNotAutoRegister]`:

```csharp
[DoNotInject]
public class AmplitudeConfiguration { }
```

## Impact if Ignored

If you ignore this warning:

1. **Runtime exception**: Services depending on the undiscovered type will throw `InvalidOperationException`:
   ```
   System.InvalidOperationException: No service for type 'AmplitudeConfiguration' has been registered.
   ```

2. **Silent failures**: The application may start but fail when the missing type is first requested.

3. **Debugging difficulty**: The root cause isn't obvious since there's no compile error.

## Why This Happens

This commonly occurs when:

1. **MSBuild conventions auto-set prefixes**: Some build configurations automatically set `IncludeNamespacePrefixes` based on the project name.

2. **Copy-paste errors**: Types copied from examples or templates may lack namespace declarations.

3. **Migration from reflection**: Reflection-based discovery scanned all types regardless of namespace. Source generation requires explicit configuration.

## Detection Criteria

This analyzer reports a warning when ALL of these conditions are true:

1. The type is in the global namespace
2. The assembly has `[GenerateTypeRegistry]` with `IncludeNamespacePrefixes` set
3. `IncludeNamespacePrefixes` doesn't include `""` (empty string)
4. The type is NOT marked with `[DoNotInject]` or `[DoNotAutoRegister]`
5. The type appears injectable:
   - Has a lifetime attribute (`[Singleton]`, `[Scoped]`, `[Transient]`)
   - Implements one or more interfaces
   - Has a constructor with interface/abstract class parameters

## When to Suppress

Suppress this warning if:

- The type is intentionally not registered with DI
- You're manually registering the type elsewhere
- The type is only used as a data transfer object (DTO)

```csharp
#pragma warning disable NDLRCOR004
public class MyGlobalType { }  // Not intended for DI
#pragma warning restore NDLRCOR004
```

Or in your project file:
```xml
<NoWarn>$(NoWarn);NDLRCOR004</NoWarn>
```

## See Also

- [Advanced Usage - Namespace Filtering](../advanced-usage.md#namespace-filtering)
- [NDLRCOR001: Plugin constructor dependency not registered](NDLRCOR001.md)
- [Core Concepts - Type Discovery](../core-concepts.md#type-discovery)
