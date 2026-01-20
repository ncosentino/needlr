# NDLRCOR001: Reflection API used in AOT project

## Cause

You are using a reflection-based Needlr API in a project that has AOT (Ahead-of-Time) compilation or trimming enabled.

## Rule Description

Native AOT and trimmed applications cannot use reflection reliably. The Needlr library provides source-generation-based alternatives for all reflection-based components. Using reflection APIs in an AOT-enabled project will cause runtime failures.

This analyzer triggers when you use any of the following in a project with `PublishAot=true` or `IsAotCompatible=true`:

### Reflection Types
- `ReflectionPluginFactory`
- `ReflectionTypeRegistrar`
- `ReflectionTypeFilterer`
- `ReflectionAssemblyLoader`
- `ReflectionAssemblyProvider`
- `ReflectionServiceProviderBuilder`

### Reflection Extension Methods
- `UsingReflectionTypeRegistrar()`
- `UsingReflectionTypeFilterer()`
- `UsingReflectionPluginFactory()`
- `UsingReflectionAssemblyLoader()`
- `UsingReflectionAssemblyProvider()`

## How to Fix

Replace reflection-based APIs with their source-generation equivalents:

### Before (Reflection)
```csharp
var app = new Syringe()
    .UsingReflection()
    .ForWebApplication()
    .BuildWebApplication();
```

### After (Source Generation)
```csharp
var app = new Syringe()
    .UsingSourceGen()
    .ForWebApplication()
    .BuildWebApplication();
```

Ensure you have the source generator packages installed:

```xml
<PackageReference Include="NexusLabs.Needlr.Generators" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
<PackageReference Include="NexusLabs.Needlr.Generators.Attributes" />
<PackageReference Include="NexusLabs.Needlr.Injection.SourceGen" />
```

## When to Suppress

Only suppress this warning if you are certain the reflection code path will not be executed at runtime, such as in conditional compilation scenarios where the reflection code is only used in non-AOT builds.

```csharp
#pragma warning disable NDLRCOR001
// Reflection code that won't run in AOT
#pragma warning restore NDLRCOR001
```

## See Also

- [Getting Started Guide](../getting-started.md)
- [Source Generation vs Reflection](../core-concepts.md)
