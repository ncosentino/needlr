# NDLRGEN001: Internal type in referenced assembly cannot be registered

## Cause

You are scanning a namespace that contains internal types in a referenced assembly, but those types cannot be registered because they are not accessible from the generated code.

## Rule Description

When using source generation with `[GenerateTypeRegistry]`, the generator scans specified namespace prefixes to discover types for automatic registration. If an internal type in a referenced assembly matches the namespace filter and would otherwise be registerable (e.g., implements an interface or is a plugin), the generator cannot access it.

This error indicates a configuration problem that would cause silent runtime failures if not addressed.

## How to Fix

You have two options:

### Option 1: Add [GenerateTypeRegistry] to the Referenced Assembly

Add the `[GenerateTypeRegistry]` attribute to the assembly containing the internal types. This allows that assembly to generate its own type registry that can access its internal types.

```csharp
// In the referenced assembly (e.g., MyPlugin project)
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "MyPlugin" })]

namespace MyPlugin
{
    internal class MyInternalService : IMyService { }
}
```

### Option 2: Make the Type Public

If the type can be made public, change its accessibility:

```csharp
namespace MyPlugin
{
    public class MyService : IMyService { }
}
```

## When to Suppress

Only suppress this error if you intentionally don't want the internal type to be registered:

```csharp
#pragma warning disable NDLRGEN001
// Intentionally not registering this internal type
#pragma warning restore NDLRGEN001
```

## Example

### Code with Error

```csharp
// HostApp project
using NexusLabs.Needlr.Generators;

// Scanning "MyPlugin" namespace but MyPlugin assembly has internal types
[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "MyPlugin" })]
```

```csharp
// MyPlugin project (referenced by HostApp) - NO [GenerateTypeRegistry]
namespace MyPlugin
{
    public interface IMyService { }
    internal class MyInternalService : IMyService { }  // Error: Cannot be registered
}
```

### Fixed Code

```csharp
// MyPlugin project - WITH [GenerateTypeRegistry]
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "MyPlugin" })]

namespace MyPlugin
{
    public interface IMyService { }
    internal class MyInternalService : IMyService { }  // Now properly registered
}
```

## See Also

- [NDLRGEN002](NDLRGEN002.md) - Missing type registry for internal plugin types
- [Advanced Usage - Multi-Project Solutions](../advanced-usage.md#multi-project-solutions-with-source-generation)
- [Getting Started Guide](../getting-started.md)
