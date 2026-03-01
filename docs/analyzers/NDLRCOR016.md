# NDLRCOR016: [DoNotAutoRegister] on a plugin class is redundant

## Summary

A class that implements a Needlr plugin interface has `[DoNotAutoRegister]` applied directly. This attribute is already present on the plugin interface itself and does not need to be repeated on implementing classes.

## Description

`[DoNotAutoRegister]` tells the Needlr source generator to skip automatic DI service registration for the decorated type. Plugin interfaces like `IServiceCollectionPlugin`, `IWebApplicationPlugin`, and others already carry this attribute so that Needlr does not register the interface type itself as a DI service.

Applying `[DoNotAutoRegister]` directly to a class that implements one of these interfaces is redundant. More importantly, in older versions of Needlr this pattern accidentally prevented the plugin class from being discovered at all (see fix for `IsPluginType`). Removing the attribute from the implementing class is the correct fix.

## Severity

**Warning** - The code compiles, but the attribute is redundant and may indicate a misunderstanding of how the attribute is intended to work.

## Example

### Invalid Code

```csharp
// ⚠️ Warning: [DoNotAutoRegister] on the implementing class is redundant.
// The attribute is already on IServiceCollectionPlugin.
[DoNotAutoRegister]
public class MyPlugin : IServiceCollectionPlugin
{
    public void Configure(ServiceCollectionPluginOptions options)
    {
        options.Services.AddSingleton<IMyService, MyService>();
    }
}
```

### Valid Code

```csharp
// ✅ OK: [DoNotAutoRegister] is not needed here.
// IServiceCollectionPlugin already carries it.
public class MyPlugin : IServiceCollectionPlugin
{
    public void Configure(ServiceCollectionPluginOptions options)
    {
        options.Services.AddSingleton<IMyService, MyService>();
    }
}
```

### Valid usage on a non-plugin class

```csharp
// ✅ OK: applying [DoNotAutoRegister] to a class that does not implement
// a plugin interface is the intended usage — it prevents DI auto-registration.
[DoNotAutoRegister]
public class InternalHelper
{
    // ...
}
```

## How to Fix

Remove `[DoNotAutoRegister]` from the plugin class. The plugin interface already carries the attribute; it does not need to be repeated.

## When to Suppress

Suppress only if you are intentionally targeting an older version of Needlr that has the `IsPluginType` bug and need `[DoNotAutoRegister]` on the class as a workaround. In all current versions, removing the attribute is the correct fix.

## See Also

- [Plugin Development](../plugin-development.md)
- [NDLRCOR001](NDLRCOR001.md)
