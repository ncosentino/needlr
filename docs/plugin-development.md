---
description: Build modular .NET applications with Needlr plugins -- implement IServiceCollectionPlugin and IWebApplicationPlugin to encapsulate service registration and configuration in reusable units.
---

# Plugin Development Guide

Plugins are a powerful way to extend Needlr's functionality and configure your application in a modular, reusable way.

## Plugin Types

Needlr provides several main plugin interfaces, each serving a specific purpose in the application lifecycle. They are
intended to be split out into different packages as necessary with some in the core set of packages and others split
out into others so you can control what you incorporate.

### 1. IServiceCollectionPlugin

Configures services during the initial registration phase.

```csharp
using Microsoft.Extensions.DependencyInjection;
using NexusLabs.Needlr;

public class DatabasePlugin : IServiceCollectionPlugin
{
    public void Configure(ServiceCollectionPluginOptions options)
    {
        // Access the service collection
        options.Services.AddDbContext<MyDbContext>(opts =>
            opts.UseSqlServer(options.Configuration.GetConnectionString("Default")));
        
        // Log registration activities
        options.Logger.LogInformation("Database context registered");
        
        // Access configuration
        var connectionString = options.Configuration.GetConnectionString("Default");
        
        // Register additional services
        options.Services.AddScoped<IRepository, Repository>();
    }
}
```

### 2. IPostBuildServiceCollectionPlugin

Executes after the main service collection has been built, useful for validation or late configuration.

```csharp
public class ValidationPlugin : IPostBuildServiceCollectionPlugin
{
    public void Configure(PostBuildServiceCollectionPluginOptions options)
    {
        // Validate that required services are registered
        var requiredService = options.ServiceProvider.GetService<IRequiredService>();
        if (requiredService == null)
        {
            options.Logger.LogError("IRequiredService is not registered!");
            throw new InvalidOperationException("Required service missing");
        }
        
        // Perform post-build configuration
        var configService = options.ServiceProvider.GetRequiredService<IConfigurationService>();
        configService.Validate();
        
        options.Logger.LogInformation("Post-build validation completed");
    }
}
```

### 3. IWebApplicationBuilderPlugin

Configures the WebApplicationBuilder before the application is built.

```csharp
public class SecurityPlugin : IWebApplicationBuilderPlugin
{
    public void Configure(WebApplicationBuilderPluginOptions options)
    {
        // Configure services
        options.Builder.Services.AddAuthentication()
            .AddJwtBearer(opts => 
            {
                opts.Authority = options.Builder.Configuration["Auth:Authority"];
            });
        
        options.Builder.Services.AddAuthorization(opts =>
        {
            opts.AddPolicy("AdminOnly", policy => 
                policy.RequireRole("Admin"));
        });
        
        // Configure Kestrel
        options.Builder.WebHost.ConfigureKestrel(serverOptions =>
        {
            serverOptions.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10MB
        });
        
        // Add configuration sources
        options.Builder.Configuration.AddJsonFile("security.json", optional: true);
        
        options.Logger.LogInformation("Security configured");
    }
}
```

### 4. IWebApplicationPlugin

Configures the WebApplication after it's built, typically for middleware and endpoint configuration.

```csharp
public class ApiPlugin : IWebApplicationPlugin
{
    public void Configure(WebApplicationPluginOptions options)
    {
        var app = options.WebApplication;
        
        // Configure middleware pipeline
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }
        
        app.UseAuthentication();
        app.UseAuthorization();
        
        // Map endpoints
        app.MapGet("/api/health", () => Results.Ok(new { Status = "Healthy" }))
            .WithName("HealthCheck")
            .WithOpenApi();
        
        app.MapPost("/api/data", async (DataRequest request, IDataService service) =>
        {
            var result = await service.ProcessAsync(request);
            return Results.Ok(result);
        })
        .RequireAuthorization("AdminOnly");
        
        options.Logger.LogInformation("API endpoints configured");
    }
}
```

## Plugin Discovery and Registration

### Automatic Discovery

Plugins are automatically discovered through assembly scanning. You must configure a discovery strategy:

```csharp
// With source generation
var webApp = new Syringe()
    .UsingSourceGen()
    .ForWebApplication()
    .BuildWebApplication();

// With reflection
var webApp = new Syringe()
    .UsingReflection()
    .ForWebApplication()
    .BuildWebApplication();
```

For built-in plugins, you do not need to annotate them with the special
attributes that prevent auto-registration and injection prevention since
this is done by the framework itself.

> **Second source generators:** If a second source generator in your assembly emits plugin types,
> `TypeRegistryGenerator` cannot see them — it operates on the original compilation. Use
> `NeedlrSourceGenBootstrap.RegisterPlugins()` from a `[ModuleInitializer]` to register those types
> at runtime. See [Cross-Generator Plugins](cross-generator-plugins.md).

### How Cross-Assembly Plugin Discovery Works

Plugins often live in separate NuGet packages (e.g., `NexusLabs.Needlr.Carter`,
`NexusLabs.Needlr.Serilog`). Needlr discovers these plugins automatically, but
the mechanism differs between reflection and source-generation modes.

#### Source-Generation Mode (`UsingSourceGen`)

Every assembly that references the Needlr source generator and has
`[GenerateTypeRegistry]` gets a compile-time `[ModuleInitializer]` emitted by
the generator. This initializer calls `NeedlrSourceGenBootstrap.Register()` to
register that assembly's types and plugins into a shared, append-only registry.

```
Assembly load order (CLR guarantees all [ModuleInitializer] methods complete
before any application code runs):

  NexusLabs.Needlr.Carter.dll loaded
    └─ [ModuleInitializer] → Register(CarterWebApplicationBuilderPlugin, ...)

  NexusLabs.Needlr.Serilog.dll loaded
    └─ [ModuleInitializer] → Register(SerilogPlugin, ...)

  MyApp.dll loaded
    └─ [ModuleInitializer] → Register(MyPlugin, MyService, ...)

  Application code starts
    └─ new Syringe().UsingSourceGen()  ← sees ALL plugins from ALL assemblies
```

When `UsingSourceGen()` is called, it reads from `NeedlrSourceGenBootstrap` which
contains the merged set of types and plugins from every registered assembly. No
reflection, no assembly scanning, fully AOT-compatible.

**Key requirement**: the package that ships the plugin needs two things:

1. **Reference the generator** so Roslyn runs it during compilation
2. **Add `[assembly: GenerateTypeRegistry]`** so the generator knows to emit a
   `TypeRegistry` and `[ModuleInitializer]` for this assembly

The generator reference alone is not enough — without the assembly attribute,
the generator has no trigger to emit anything.

For project references (developing within the Needlr solution), add both to
the `.csproj` and create a `GeneratorAssemblyInfo.cs`:

```xml
<!-- .csproj -->
<ItemGroup>
  <ProjectReference Include="..\NexusLabs.Needlr.Generators\NexusLabs.Needlr.Generators.csproj"
                    OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
  <ProjectReference Include="..\NexusLabs.Needlr.Generators.Attributes\NexusLabs.Needlr.Generators.Attributes.csproj" />
</ItemGroup>
```

```csharp
// GeneratorAssemblyInfo.cs
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "MyCompany.MyPlugin" })]
```

For NuGet consumers, the `NexusLabs.Needlr.Build` package handles both
automatically — the `.targets` file emits the assembly attribute:

```xml
<PackageReference Include="NexusLabs.Needlr.Build" PrivateAssets="all" />
```

Without both pieces, the assembly has no `[ModuleInitializer]` and its plugins
are invisible to `UsingSourceGen()`. See `NexusLabs.Needlr.Carter` and
`NexusLabs.Needlr.Serilog` for working examples of this pattern.

#### Reflection Mode (`UsingReflection`)

The reflection scanner loads assemblies from the application's base directory
and scans them for types implementing plugin interfaces. Assemblies from
referenced NuGet packages are in the output directory and are scanned
automatically.

If a referenced assembly is not in the default scan path (e.g., it's loaded
lazily or from a non-standard location), use `UsingAdditionalAssemblies` to
include it explicitly:

```csharp
new Syringe()
    .UsingReflection()
    .UsingAdditionalAssemblies([typeof(SerilogPlugin).Assembly])
    .BuildServiceProvider(config);
```

#### Why This Matters for Package Authors

If you're building a Needlr integration package that ships plugins:

1. Reference the Needlr generator in your `.csproj`
2. Add a `GeneratorAssemblyInfo.cs` with `[assembly: GenerateTypeRegistry(...)]`
   scoped to your package's namespace
3. Make plugin classes `public` (the generator emits NDLRGEN002 if an internal
   plugin is in an assembly without `[GenerateTypeRegistry]`)
4. Don't require consumers to manually register your plugins — the
   `[ModuleInitializer]` bootstrap handles it automatically in both modes

Missing step 2 is the most common mistake — the generator runs but produces
no output because it has no `[GenerateTypeRegistry]` trigger.

### Controlling Plugin Discovery

#### Assembly Filtering

Control which assemblies are scanned for plugins:

```csharp
var webApp = new Syringe()
    .UsingSourceGen()  // or .UsingReflection()
    .UsingAssemblyProvider(builder => builder
        .MatchingAssemblies(x => 
            x.Contains("MyCompany.Plugins"))
        .Build())
    .ForWebApplication()
    .BuildWebApplication();
```

## Plugin Execution Order

Plugins are executed in a deterministic order based on two factors:

1. **Plugin Order Attribute** - Explicit ordering via `[PluginOrder]`
2. **Type Name** - Alphabetical sorting when orders are equal

### Using the PluginOrder Attribute

Use the `[PluginOrder]` attribute to control when your plugin executes relative to others:

```csharp
using NexusLabs.Needlr;

// Executes first - negative values run before default (0)
[PluginOrder(-100)]
public class DatabaseMigrationPlugin : IServiceCollectionPlugin
{
    public void Configure(ServiceCollectionPluginOptions options)
    {
        // Run migrations before other plugins register services
        options.Services.AddDbContext<MyDbContext>();
    }
}

// Executes at default order (0) - no attribute needed
public class BusinessLogicPlugin : IServiceCollectionPlugin
{
    public void Configure(ServiceCollectionPluginOptions options)
    {
        // Normal service registration
        options.Services.AddScoped<IOrderService, OrderService>();
    }
}

// Executes last - positive values run after default (0)
[PluginOrder(100)]
public class ValidationPlugin : IServiceCollectionPlugin
{
    public void Configure(ServiceCollectionPluginOptions options)
    {
        // Validate all registrations are complete
    }
}
```

### Order Values

| Order Range | Typical Use Case |
|-------------|------------------|
| -100 to -50 | Infrastructure setup (database, logging, configuration) |
| -50 to -1 | Core services that others depend on |
| 0 (default) | Normal business logic plugins |
| 1 to 50 | Plugins that depend on other registrations |
| 50 to 100 | Validation, cleanup, and finalization |

### Same-Order Determinism

When multiple plugins have the same order value, they are sorted alphabetically by their fully qualified type name. This ensures deterministic execution across builds:

```csharp
// Both have Order = 0, so they execute alphabetically
public class AuditPlugin : IServiceCollectionPlugin { }      // Executes first
public class ZipCodePlugin : IServiceCollectionPlugin { }    // Executes second
```

### Works with All Plugin Types

The `[PluginOrder]` attribute works with all Needlr plugin interfaces:

```csharp
// IServiceCollectionPlugin
[PluginOrder(-50)]
public class EarlyServicePlugin : IServiceCollectionPlugin { }

// IWebApplicationBuilderPlugin
[PluginOrder(10)]
public class MiddlewareSetupPlugin : IWebApplicationBuilderPlugin { }

// IWebApplicationPlugin
[PluginOrder(100)]
public class FinalEndpointPlugin : IWebApplicationPlugin { }

// IHostApplicationBuilderPlugin
[PluginOrder(-100)]
public class HostSetupPlugin : IHostApplicationBuilderPlugin { }
```

### Parity Between Reflection and Source Generation

Plugin ordering works identically whether you use reflection or source generation:

```csharp
// Both produce the same plugin execution order
var reflectionApp = new Syringe()
    .UsingReflection()
    .ForWebApplication()
    .BuildWebApplication();

var sourceGenApp = new Syringe()
    .UsingSourceGen()
    .ForWebApplication()
    .BuildWebApplication();
```

### Assembly Filtering

You can also control which assemblies are scanned for plugins:

```csharp
var webApp = new Syringe()
    .UsingSourceGen()  // or .UsingReflection()
    .UsingAssemblyProvider(builder => builder
        .MatchingAssemblies(x => x.Contains("MyCompany.Plugins"))
        .UseLibTestEntryOrdering() // Libraries → Executables → Tests
        .Build())
    .ForWebApplication()
    .BuildWebApplication();
```

### Execution Timeline

Within each lifecycle phase, plugins are sorted by `[PluginOrder]` then by type name:

1. **IServiceCollectionPlugin** - During service registration
2. **IPostBuildServiceCollectionPlugin** - After service provider is built
3. **IWebApplicationBuilderPlugin** - Before WebApplication.Build()
4. **IWebApplicationPlugin** - After WebApplication.Build()

## Advanced Plugin Patterns

### Configuration-Driven Plugins

```csharp
public class FeatureTogglePlugin : IServiceCollectionPlugin
{
    public void Configure(ServiceCollectionPluginOptions options)
    {
        var features = options.Configuration.GetSection("Features");
        
        if (features.GetValue<bool>("EnableCache"))
        {
            options.Services.AddMemoryCache();
            options.Services.AddSingleton<ICacheService, MemoryCacheService>();
            options.Logger.LogInformation("Cache feature enabled");
        }
        
        if (features.GetValue<bool>("EnableMetrics"))
        {
            options.Services.AddSingleton<IMetricsService, MetricsService>();
            options.Logger.LogInformation("Metrics feature enabled");
        }
    }
}
```

### Composite Plugins

Plugins may implement multiple plugin interfaces. This is especially common for
ASP.NET plugins because we may want to configure both the builder and the web
application that is created.

```csharp
public class MicroservicePlugin : IServiceCollectionPlugin, 
    IWebApplicationBuilderPlugin, 
    IWebApplicationPlugin
{
    public void Configure(ServiceCollectionPluginOptions options)
    {
        // Register core services
        options.Services.AddHealthChecks();
        options.Services.AddHttpClient();
    }
    
    public void Configure(WebApplicationBuilderPluginOptions options)
    {
        // Configure distributed tracing
        options.Builder.Services.AddOpenTelemetry()
            .WithTracing(builder => builder
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation());
    }
    
    public void Configure(WebApplicationPluginOptions options)
    {
        // Configure middleware and endpoints
        options.WebApplication.UseHealthChecks("/health");
        options.WebApplication.MapMetrics();
    }
}
```

### Plugin with Dependencies

The built-in plugins do not support dependency injection through their constructors,
so if you need dependencies then you will need to access them from the dependency
injection framework based on the lifecycle.

```csharp
public class DependentPlugin : IPostBuildServiceCollectionPlugin
{
    public void Configure(PostBuildServiceCollectionPluginOptions options)
    {
        // Get services that were registered by other plugins
        var dbContext = options.ServiceProvider.GetRequiredService<MyDbContext>();
        var cache = options.ServiceProvider.GetService<IMemoryCache>();
        
        if (cache != null)
        {
            // Initialize cache with data from database
            var initialData = dbContext.Settings.ToList();
            foreach (var setting in initialData)
            {
                cache.Set(setting.Key, setting.Value);
            }
        }
    }
}
```
