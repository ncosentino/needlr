# NDLRGEN020: [Options] is not compatible with AOT

## Cause

A class has the `[Options]` attribute but is in a project with `PublishAot=true` or `IsAotCompatible=true`.

## Rule Description

The `[Options]` attribute generates code that calls Microsoft's configuration binding APIs:
- `Configure<T>()`
- `BindConfiguration()`
- `ValidateDataAnnotations()`

These APIs use **reflection** to bind configuration values to properties at runtime. They are marked with `[RequiresDynamicCode]` and `[RequiresUnreferencedCode]`, making them incompatible with:
- **Native AOT** - No JIT compiler to generate code at runtime
- **Trimming** - Reflection targets may be trimmed away

## How to Fix

You have two options:

### Option 1: Remove [Options] from AOT Projects

If you're building a plugin or library that must be AOT-compatible, don't use `[Options]`:

```csharp
// ❌ Won't work in AOT
[Options]
public class CacheSettings
{
    public int TimeoutSeconds { get; set; }
}

// ✅ Manual configuration binding
public class CacheSettings
{
    public int TimeoutSeconds { get; set; }
    
    public static CacheSettings FromConfiguration(IConfiguration config)
    {
        return new CacheSettings
        {
            TimeoutSeconds = config.GetValue<int>("Cache:TimeoutSeconds")
        };
    }
}
```

### Option 2: Disable AOT for the Project

If you need `[Options]` functionality, disable AOT:

```xml
<PropertyGroup>
  <!-- Remove or set to false -->
  <PublishAot>false</PublishAot>
  <IsAotCompatible>false</IsAotCompatible>
</PropertyGroup>
```

## Example

### Code with Error

```csharp
using NexusLabs.Needlr.Generators;

// In a project with <PublishAot>true</PublishAot>

// NDLRGEN020: Type 'DatabaseOptions' has [Options] attribute but is in an 
//             AOT-enabled project
[Options]
public class DatabaseOptions
{
    public string ConnectionString { get; set; } = "";
}
```

### Fixed Code (Manual Binding)

```csharp
// Remove [Options] and bind manually
public class DatabaseOptions
{
    public string ConnectionString { get; set; } = "";
}

// In your startup code:
public static class ServiceRegistration
{
    public static void RegisterOptions(IServiceCollection services, IConfiguration config)
    {
        var dbOptions = new DatabaseOptions
        {
            ConnectionString = config["Database:ConnectionString"] ?? ""
        };
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(dbOptions));
    }
}
```

## Why This Limitation Exists

Microsoft's Configuration Binding Generator (`<EnableConfigurationBindingGenerator>`) uses C# **interceptors** to replace reflection-based calls with compile-time generated code. However:

1. Interceptors can only intercept calls in **hand-written source code**
2. Needlr's `[Options]` generates the `Configure<T>()` calls in `TypeRegistry.g.cs`
3. One source generator **cannot intercept another's output**

Therefore, the reflection-based calls cannot be replaced with AOT-safe alternatives.

## Future Considerations

A future version of Needlr may generate AOT-compatible binding code directly, similar to Microsoft's approach. Until then, manual configuration binding is required for AOT projects.

## Affected Project Properties

This error is triggered when **either** of these MSBuild properties is `true`:

| Property | Purpose |
|----------|---------|
| `PublishAot` | Enables Native AOT publishing |
| `IsAotCompatible` | Marks a library as AOT-compatible |

## See Also

- [Microsoft Docs: Native AOT Deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)
- [Microsoft Docs: Configuration Binding Generator](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration-generator)
- [Options Documentation](../options.md)
