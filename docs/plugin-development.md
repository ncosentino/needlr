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

Plugins are executed in the order they're discovered, which follows the assembly sorting configuration:

```csharp
var webApp = new Syringe()
    .UsingSourceGen()  // or .UsingReflection()
    .UsingAssemblyProvider(builder => builder
        .UseLibTestEntrySorting() // Libraries → Tests → Entry assembly
        .Build())
    .ForWebApplication()
    .BuildWebApplication();
```

### Execution Timeline

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
