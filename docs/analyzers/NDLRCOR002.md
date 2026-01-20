# NDLRCOR002: Plugin has constructor dependencies

## Cause

A class implementing `IServiceCollectionPlugin` or `IPostBuildServiceCollectionPlugin` has a constructor with parameters but no public parameterless constructor.

## Rule Description

Needlr plugin classes are instantiated by the framework before the dependency injection container is fully built. This means constructor injection is not available for plugin classes in the same way it is for regular services.

If a plugin has constructor parameters and no parameterless constructor, the framework may not be able to instantiate it, leading to runtime errors.

## How to Fix

### Option 1: Add a parameterless constructor

```csharp
public class MyPlugin : IServiceCollectionPlugin
{
    public MyPlugin() { }
    
    public MyPlugin(ILogger logger) 
    { 
        // Optional: for use when instantiated via DI
    }
    
    public void Configure(ServiceCollectionPluginOptions options)
    {
        // Plugin configuration
    }
}
```

### Option 2: Use IPostBuildServiceCollectionPlugin with service resolution

If you need access to services, use `IPostBuildServiceCollectionPlugin` which runs after the container is built:

```csharp
public class MyPlugin : IPostBuildServiceCollectionPlugin
{
    public void Configure(PostBuildServiceCollectionPluginOptions options)
    {
        var logger = options.ServiceProvider.GetRequiredService<ILogger<MyPlugin>>();
        // Use the logger
    }
}
```

### Option 3: Access services through the options parameter

```csharp
public class MyPlugin : IServiceCollectionPlugin
{
    public void Configure(ServiceCollectionPluginOptions options)
    {
        // Register your service that needs dependencies
        options.Services.AddSingleton<MyService>();
    }
}
```

## When to Suppress

Suppress this warning if:
- The plugin is intentionally designed to be instantiated via DI after container construction
- The plugin is abstract and constructor parameters are for derived classes
- You are using a custom plugin factory that handles constructor injection

```csharp
#pragma warning disable NDLRCOR002
public class MyCustomPlugin : IServiceCollectionPlugin
{
    public MyCustomPlugin(IPluginFactory factory) { }
    // ...
}
#pragma warning restore NDLRCOR002
```

## See Also

- [Plugin Development Guide](../plugin-development.md)
- [Core Concepts](../core-concepts.md)
