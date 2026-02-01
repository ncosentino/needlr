# NDLRGEN002: Referenced assembly has internal plugin types but no type registry

## Cause

A referenced assembly contains internal types that implement Needlr plugin interfaces (e.g., `IServiceCollectionPlugin`), but the assembly does not have a `[GenerateTypeRegistry]` attribute.

## Rule Description

When using source generation, internal plugin types in referenced assemblies cannot be discovered by the host application's generator. This means internal plugins will silently fail to load at runtime.

This error proactively detects this misconfiguration at compile time, preventing runtime failures.

### Plugin Interfaces Checked

The generator checks for internal types implementing any of these interfaces:


- `NexusLabs.Needlr.IServiceCollectionPlugin`
- `NexusLabs.Needlr.IPostBuildServiceCollectionPlugin`
- `NexusLabs.Needlr.IWebApplicationPlugin`
- `NexusLabs.Needlr.IWebApplicationBuilderPlugin`
- `NexusLabs.Needlr.SignalR.IHubRegistrationPlugin`
- `NexusLabs.Needlr.SemanticKernel.IKernelBuilderPlugin`

## How to Fix

You have two options:

### Option 1: Add [GenerateTypeRegistry] to the Plugin Assembly

Add the `[GenerateTypeRegistry]` attribute to the assembly containing the internal plugin:

```csharp
// In the plugin assembly (e.g., MyPlugin project)
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "MyPlugin" })]

namespace MyPlugin
{
    internal class MyPlugin : IServiceCollectionPlugin
    {
        public void Configure(ServiceCollectionPluginOptions options) { }
    }
}
```

### Option 2: Make the Plugin Type Public

If the plugin can be made public, change its accessibility:

```csharp
namespace MyPlugin
{
    public class MyPlugin : IServiceCollectionPlugin
    {
        public void Configure(ServiceCollectionPluginOptions options) { }
    }
}
```

## When to Suppress

Only suppress this error if you intentionally don't want the plugin to be discovered:

```csharp
#pragma warning disable NDLRGEN002
// Intentionally keeping this plugin internal and unregistered
#pragma warning restore NDLRGEN002
```

## Example

### Code with Error

```csharp
// HostApp project
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]
```

```csharp
// MyPlugin project (referenced by HostApp) - NO [GenerateTypeRegistry]
namespace MyPlugin
{
    internal class AuthenticationPlugin : IWebApplicationBuilderPlugin
    {
        public void Configure(WebApplicationBuilderPluginOptions options)
        {
            options.Builder.Services.AddAuthentication();
        }
    }
}
```

**Result**: `NDLRGEN002` error - AuthenticationPlugin will not be discovered at runtime.

### Fixed Code

```csharp
// MyPlugin project - WITH [GenerateTypeRegistry]
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "MyPlugin" })]

namespace MyPlugin
{
    internal class AuthenticationPlugin : IWebApplicationBuilderPlugin
    {
        public void Configure(WebApplicationBuilderPluginOptions options)
        {
            options.Builder.Services.AddAuthentication();
        }
    }
}
```

## Multi-Project Solutions

For solutions with many plugin projects, consider using MSBuild conventions to automatically add `[GenerateTypeRegistry]` to matching projects. See [Advanced Usage - Multi-Project Solutions](../advanced-usage.md#multi-project-solutions-with-source-generation) for details.

## See Also

- [NDLRGEN001](NDLRGEN001.md) - Internal type in referenced assembly cannot be registered
- [Advanced Usage - Multi-Project Solutions](../advanced-usage.md#multi-project-solutions-with-source-generation)
- [Getting Started Guide](../getting-started.md)
