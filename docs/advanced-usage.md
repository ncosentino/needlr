---
description: Advanced patterns for Needlr -- custom assembly scanning, conditional registration, multi-project setups, and edge cases for complex .NET dependency injection scenarios.
---

# Advanced Usage

This guide covers advanced scenarios and techniques for using Needlr in complex applications.

> **Note**: Many advanced features require reflection. If you're building an AOT application, 
> stick to the source-generation patterns described in the [Getting Started](getting-started.md) guide.

## Custom Type Registrars

### Implementing ITypeRegistrar

Create custom registration logic by implementing `ITypeRegistrar`. This is typically used with reflection:

```csharp
using Microsoft.Extensions.DependencyInjection;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

public class ConventionBasedTypeRegistrar : ITypeRegistrar
{
    public void RegisterTypes(
        IServiceCollection services,
        IEnumerable<Type> types,
        ILogger logger)
    {
        foreach (var type in types)
        {
            // Register repositories as scoped
            if (type.Name.EndsWith("Repository"))
            {
                var interfaces = type.GetInterfaces();
                foreach (var @interface in interfaces)
                {
                    services.AddScoped(@interface, type);
                    logger.LogDebug($"Registered {type.Name} as {@interface.Name} (Scoped)");
                }
            }
            // Register services as transient
            else if (type.Name.EndsWith("Service"))
            {
                var interfaces = type.GetInterfaces();
                foreach (var @interface in interfaces)
                {
                    services.AddTransient(@interface, type);
                    logger.LogDebug($"Registered {type.Name} as {@interface.Name} (Transient)");
                }
            }
            // Register singletons for specific patterns
            else if (type.GetInterfaces().Any(i => i.Name == "ISingleton"))
            {
                services.AddSingleton(type);
                logger.LogDebug($"Registered {type.Name} as Singleton");
            }
        }
    }
}

// Usage (requires reflection strategy)
var serviceProvider = new Syringe()
    .UsingReflection()
    .UsingTypeRegistrar(new ConventionBasedTypeRegistrar())
    .BuildServiceProvider();
```

## Custom Type Filterers

### Implementing ITypeFilterer

Control which types are eligible for registration:

```csharp
public class NamespaceTypeFilterer : ITypeFilterer
{
    private readonly string[] _allowedNamespaces;
    
    public NamespaceTypeFilterer(params string[] allowedNamespaces)
    {
        _allowedNamespaces = allowedNamespaces;
    }
    
    public IEnumerable<Type> Filter(IEnumerable<Type> types)
    {
        return types.Where(type =>
        {
            // Skip if no namespace
            if (type.Namespace == null)
                return false;
            
            // Check if in allowed namespaces
            var isAllowed = _allowedNamespaces.Any(ns => 
                type.Namespace.StartsWith(ns));
            
            // Also exclude test classes
            var isTest = type.Name.EndsWith("Test") || 
                         type.Name.EndsWith("Tests");
            
            return isAllowed && !isTest;
        });
    }
}

// Usage (requires reflection strategy)
var serviceProvider = new Syringe()
    .UsingReflection()
    .UsingTypeFilterer(new NamespaceTypeFilterer(
        "MyCompany.Core",
        "MyCompany.Services",
        "MyCompany.Data"))
    .BuildServiceProvider();
```

### Chaining Type Filterers

Use `TypeFilterDecorator` to chain multiple filters:

```csharp
public class CompositeFilterer : ITypeFilterer
{
    private readonly ITypeFilterer[] _filters;
    
    public CompositeFilterer(params ITypeFilterer[] filters)
    {
        _filters = filters;
    }
    
    public IEnumerable<Type> Filter(IEnumerable<Type> types)
    {
        var result = types;
        foreach (var filter in _filters)
        {
            result = filter.Filter(result);
        }
        return result;
    }
}

// Usage (requires reflection strategy)
var serviceProvider = new Syringe()
    .UsingReflection()
    .UsingTypeFilterer(new CompositeFilterer(
        new ReflectionTypeFilterer(),
        new NamespaceTypeFilterer("MyCompany"),
        new AttributeTypeFilterer<ObsoleteAttribute>(exclude: true)))
    .BuildServiceProvider();
```

## Complex Decorator Patterns

### Nested Decorators with Attributes (Recommended)

The simplest way to apply multiple decorators is using the `[DecoratorFor<T>]` attribute:

```csharp
// Base service - registered automatically
public class DataService : IDataService
{
    public async Task<Data> GetDataAsync()
    {
        return await FetchFromDatabase();
    }
}

// Caching decorator - Order 1 means closest to original
[DecoratorFor<IDataService>(Order = 1)]
public class CachingDataService : IDataService
{
    private readonly IDataService _inner;
    private readonly IMemoryCache _cache;
    
    public CachingDataService(IDataService inner, IMemoryCache cache)
    {
        _inner = inner;
        _cache = cache;
    }
    
    public async Task<Data> GetDataAsync()
    {
        return await _cache.GetOrCreateAsync("data", 
            async entry => await _inner.GetDataAsync());
    }
}

// Logging decorator - Order 2 wraps the caching decorator
[DecoratorFor<IDataService>(Order = 2)]
public class LoggingDataService : IDataService
{
    private readonly IDataService _inner;
    private readonly ILogger<LoggingDataService> _logger;
    
    public LoggingDataService(IDataService inner, ILogger<LoggingDataService> logger)
    {
        _inner = inner;
        _logger = logger;
    }
    
    public async Task<Data> GetDataAsync()
    {
        _logger.LogInformation("Fetching data...");
        var data = await _inner.GetDataAsync();
        _logger.LogInformation($"Fetched {data.Count} items");
        return data;
    }
}

// No plugin needed! Resolution produces:
// LoggingDataService → CachingDataService → DataService
```

### Nested Decorators (Manual)

For more control, apply decorators manually in a plugin:

```csharp
// Base service
[DoNotAutoRegister]
public class DataService : IDataService
{
    public async Task<Data> GetDataAsync()
    {
        // Fetch from database
        return await FetchFromDatabase();
    }
}

// Caching decorator
[DoNotAutoRegister]
public class CachingDataService : IDataService
{
    private readonly IDataService _inner;
    private readonly IMemoryCache _cache;
    
    public CachingDataService(IDataService inner, IMemoryCache cache)
    {
        _inner = inner;
        _cache = cache;
    }
    
    public async Task<Data> GetDataAsync()
    {
        return await _cache.GetOrCreateAsync("data", 
            async entry => await _inner.GetDataAsync());
    }
}

// Logging decorator
[DoNotAutoRegister]
public class LoggingDataService : IDataService
{
    private readonly IDataService _inner;
    private readonly ILogger<LoggingDataService> _logger;
    
    public LoggingDataService(IDataService inner, ILogger<LoggingDataService> logger)
    {
        _inner = inner;
        _logger = logger;
    }
    
    public async Task<Data> GetDataAsync()
    {
        _logger.LogInformation("Fetching data...");
        var data = await _inner.GetDataAsync();
        _logger.LogInformation($"Fetched {data.Count} items");
        return data;
    }
}

// Registration plugin
public class DataServicePlugin : IServiceCollectionPlugin
{
    public void Configure(ServiceCollectionPluginOptions options)
    {
        // Register base service
        options.Services.AddScoped<DataService>();
        
        // Apply decorators in order (innermost to outermost)
        options.Services.AddScoped<IDataService>(sp =>
        {
            IDataService service = sp.GetRequiredService<DataService>();
            service = new CachingDataService(service, sp.GetRequiredService<IMemoryCache>());
            service = new LoggingDataService(service, sp.GetRequiredService<ILogger<LoggingDataService>>());
            return service;
        });
    }
}
```

### Conditional Decorators

Apply decorators based on configuration:

```csharp
public class ConditionalDecoratorPlugin : IServiceCollectionPlugin
{
    public void Configure(ServiceCollectionPluginOptions options)
    {
        options.Services.AddScoped<BaseService>();
        
        options.Services.AddScoped<IService>(sp =>
        {
            IService service = sp.GetRequiredService<BaseService>();
            
            var config = sp.GetRequiredService<IConfiguration>();
            
            if (config.GetValue<bool>("Features:EnableCaching"))
            {
                service = new CachingDecorator(service, sp.GetRequiredService<IMemoryCache>());
            }
            
            if (config.GetValue<bool>("Features:EnableLogging"))
            {
                service = new LoggingDecorator(service, sp.GetRequiredService<ILogger<LoggingDecorator>>());
            }
            
            if (config.GetValue<bool>("Features:EnableMetrics"))
            {
                service = new MetricsDecorator(service, sp.GetRequiredService<IMetricsCollector>());
            }
            
            return service;
        });
    }
}
```

## Post-Plugin Registration Callbacks

The `UsingPostPluginRegistrationCallback` method provides a way to register services after all plugins have been processed. This is available on both the `Syringe` class and `CreateWebApplicationOptions`.

### Using with Syringe

Register services directly on the Syringe instance:

```csharp
var serviceProvider = new Syringe()
    .UsingPostPluginRegistrationCallback(services =>
    {
        // Override or add services after plugins
        services.AddSingleton<ICustomService, CustomService>();
        services.Configure<MyOptions>(options => 
        {
            options.EnableFeature = true;
        });
    })
    .UsingPostPluginRegistrationCallback(services =>
    {
        // You can chain multiple callbacks
        services.AddScoped<IAnotherService, AnotherService>();
    })
    .BuildServiceProvider();
```

You can also use the plural overload ```UsingPostPluginRegistrationCallbacks``` to pass in multiple callbacks.

### Using with CreateWebApplicationOptions

For web applications, add callbacks through the options using the fluent extension methods:

```csharp
var webApplication = new Syringe()
    .ForWebApplication()
    .UsingOptions(() => CreateWebApplicationOptions.Default
        .UsingPostPluginRegistrationCallback(services =>
        {
            services.AddAuthentication();
            services.AddAuthorization();
        })
        .UsingPostPluginRegistrationCallback(services =>
        {
            // Configure after authentication is added
            services.Configure<JwtBearerOptions>(options =>
            {
                options.Authority = "https://auth.example.com";
            });
        }))
    .BuildWebApplication();
```

You can also use the plural overload to add multiple callbacks at once:

```csharp
var webApplication = new Syringe()
    .ForWebApplication()
    .UsingOptions(() => CreateWebApplicationOptions.Default
        .UsingPostPluginRegistrationCallbacks(
            services => services.AddAuthentication(),
            services => services.AddAuthorization(),
            services => services.AddAntiforgery()))
    .BuildWebApplication();
```

### Common Use Cases

Post-plugin registration callbacks are particularly useful for:

1. **Overriding Plugin Registrations**: Replace a service registered by a plugin with a custom implementation
2. **Conditional Registration**: Add services based on configuration or environment
3. **Testing**: Override services with mocks or test doubles

Example of overriding a plugin registration:

```csharp
var syringe = new Syringe()
    .UsingPostPluginRegistrationCallback(services =>
    {
        // Remove the default implementation registered by a plugin
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IEmailService));
        if (descriptor != null)
        {
            services.Remove(descriptor);
        }
        
        // Add custom implementation
        services.AddSingleton<IEmailService, CustomEmailService>();
    });
```

## Custom Assembly Providers

### Implementing IAssemblyProvider

Create custom assembly discovery logic:

```csharp
public class PluginAssemblyProvider : IAssemblyProvider
{
    private readonly string _pluginDirectory;
    
    public PluginAssemblyProvider(string pluginDirectory)
    {
        _pluginDirectory = pluginDirectory;
    }
    
    public IEnumerable<Assembly> GetAssemblies()
    {
        var assemblies = new List<Assembly>();
        
        // Load assemblies from plugin directory
        if (Directory.Exists(_pluginDirectory))
        {
            var pluginFiles = Directory.GetFiles(_pluginDirectory, "*.dll");
            foreach (var file in pluginFiles)
            {
                try
                {
                    var assembly = Assembly.LoadFrom(file);
                    assemblies.Add(assembly);
                }
                catch (Exception ex)
                {
                    // Log and continue
                    Console.WriteLine($"Failed to load {file}: {ex.Message}");
                }
            }
        }
        
        // Also include current domain assemblies
        assemblies.AddRange(AppDomain.CurrentDomain.GetAssemblies());
        
        return assemblies.Distinct();
    }
}

// Usage (requires reflection strategy for dynamic assembly loading)
var serviceProvider = new Syringe()
    .UsingReflection()
    .UsingAssemblyProvider(new PluginAssemblyProvider("./plugins"))
    .BuildServiceProvider();
```

## Advanced Web Application Configuration

### Using Configuration Callback

The `UsingConfigurationCallback` method provides fine-grained control over the WebApplicationBuilder configuration:

```csharp
var webApplication = new Syringe()
    .UsingSourceGen()  // or .UsingReflection()
    .ForWebApplication()
    .UsingConfigurationCallback((builder, options) =>
    {
        // Conditional configuration based on environment
        if (builder.Environment.IsEnvironment("Test"))
        {
            // Test-specific configuration
            builder.Configuration.AddJsonFile("appsettings.Test.json", optional: false);
        }
        else
        {
            // Production configuration
            builder.Configuration
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", 
                    optional: true, reloadOnChange: true);
        }
        
        // Add environment variables with custom prefix
        builder.Configuration.AddEnvironmentVariables("MYAPP_");
        
        // Override with in-memory configuration for testing
        if (builder.Environment.IsDevelopment())
        {
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DebugMode"] = "true",
                ["DetailedErrors"] = "true"
            });
        }
        
        // Configure services before plugin registration
        builder.Services.Configure<JsonOptions>(opts =>
        {
            opts.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        });
        
        // Configure Kestrel
        builder.WebHost.ConfigureKestrel(serverOptions =>
        {
            serverOptions.Limits.MaxRequestBodySize = 50 * 1024 * 1024; // 50MB
        });
    })
    .BuildWebApplication();
```

### Custom Web Application Factory

```csharp
public class CustomWebApplicationFactory : IWebApplicationFactory
{
    public WebApplication Create(
        CreateWebApplicationOptions options,
        Func<WebApplicationBuilder> createWebApplicationBuilderCallback)
    {
        var builder = createWebApplicationBuilderCallback();
        
        // Custom Kestrel configuration
        builder.WebHost.ConfigureKestrel(serverOptions =>
        {
            serverOptions.ListenAnyIP(5000, listenOptions =>
            {
                listenOptions.Protocols = HttpProtocols.Http2;
            });
            
            serverOptions.ListenAnyIP(5001, listenOptions =>
            {
                listenOptions.UseHttps();
                listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
            });
        });
        
        // Custom service configuration
        builder.Services.Configure<KestrelServerOptions>(options =>
        {
            options.AllowSynchronousIO = true;
        });
        
        // Add custom configuration sources
        builder.Configuration.AddJsonFile("custom-settings.json", optional: true);
        builder.Configuration.AddEnvironmentVariables("MYAPP_");
        
        // Custom logging
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.AddDebug();
        
        var app = builder.Build();
        
        // Custom middleware pipeline
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/error");
            app.UseHsts();
        }
        
        app.UseHttpsRedirection();
        app.UseResponseCompression();
        
        return app;
    }
}

// Usage
var webApp = new Syringe()
    .ForWebApplication()
    .UsingWebApplicationFactory<CustomWebApplicationFactory>()
    .BuildWebApplication();
```

### Combining Configuration Methods

You can combine multiple configuration methods for maximum flexibility:

```csharp
// With reflection and Scrutor
var webApp = new Syringe()
    .UsingReflection()
    .UsingScrutorTypeRegistrar()
    .UsingAssemblyProvider(builder => builder
        .MatchingAssemblies(x => x.Contains("MyApp"))
        .UseLibTestEntryOrdering()
        .Build())
    .ForWebApplication()
    .UsingOptions(() => CreateWebApplicationOptions
        .Default
        .UsingStartupConsoleLogger()
        .UsingApplicationName("MyApp"))
    .UsingConfigurationCallback((builder, options) =>
    {
        // Fine-tune the configuration
        builder.Configuration.SetBasePath(AppContext.BaseDirectory);
        builder.Configuration.AddUserSecrets<Program>();
        
        // Add services that plugins might depend on
        builder.Services.AddSingleton<IConfigurationValidator, ConfigurationValidator>();
    })
    .BuildWebApplication();

// With source generation
var webApp = new Syringe()
    .UsingSourceGen()
    .UsingAssemblyProvider(builder => builder
        .MatchingAssemblies(x => x.Contains("MyApp"))
        .Build())
    .ForWebApplication()
    .UsingOptions(() => CreateWebApplicationOptions
        .Default
        .UsingStartupConsoleLogger())
    .BuildWebApplication();
```

## Delayed Resolution

### Lazy Service Resolution

```csharp
public class LazyServicePlugin : IServiceCollectionPlugin
{
    public void Configure(ServiceCollectionPluginOptions options)
    {
        // Register lazy wrapper for expensive services
        options.Services.AddSingleton<Lazy<IExpensiveService>>(sp =>
            new Lazy<IExpensiveService>(() => 
                sp.GetRequiredService<IExpensiveService>()));
        
        // Register the actual expensive service
        options.Services.AddSingleton<IExpensiveService, ExpensiveService>();
    }
}

// Usage in a consumer
public class ServiceConsumer
{
    private readonly Lazy<IExpensiveService> _expensiveService;
    
    public ServiceConsumer(Lazy<IExpensiveService> expensiveService)
    {
        _expensiveService = expensiveService;
    }
    
    public void UseServiceIfNeeded(bool condition)
    {
        if (condition)
        {
            // Service is only instantiated when actually needed
            _expensiveService.Value.DoExpensiveWork();
        }
    }
}
```

## Testing Strategies

### Integration Testing with Custom Configuration

```csharp
public class IntegrationTestBase
{
    protected IServiceProvider CreateServiceProvider(
        Action<Syringe> configureSyringe = null)
    {
        var syringe = new Syringe()
            .UsingReflection()  // Reflection often useful for testing flexibility
            .UsingAssemblyProvider(builder => builder
                .MatchingAssemblies(x => x.Contains("MyApp"))
                .Build())
            .UsingConfiguration(config => config
                .AddJsonFile("appsettings.test.json")
                .AddEnvironmentVariables("TEST_"));
        
        configureSyringe?.Invoke(syringe);
        
        return syringe.BuildServiceProvider();
    }
}

public class ServiceIntegrationTests : IntegrationTestBase
{
    [Fact]
    public void Service_WithTestConfiguration_WorksCorrectly()
    {
        // Arrange
        var serviceProvider = CreateServiceProvider(syringe =>
            syringe.UsingPostPluginRegistrationCallback(services =>
            {
                // Override specific services for testing
                services.AddSingleton<IExternalService, MockExternalService>();
            }));
        
        // Act
        var service = serviceProvider.GetRequiredService<IMyService>();
        var result = service.DoWork();
        
        // Assert
        Assert.NotNull(result);
    }
}
```

## Troubleshooting

### Debugging Service Registration

```csharp
public class DiagnosticPlugin : IPostBuildServiceCollectionPlugin
{
    public void Configure(PostBuildServiceCollectionPluginOptions options)
    {
        var services = options.Services;
        
        // Log all registered services
        foreach (var service in services)
        {
            options.Logger.LogDebug(
                $"Service: {service.ServiceType.Name}, " +
                $"Implementation: {service.ImplementationType?.Name ?? "Factory"}, " +
                $"Lifetime: {service.Lifetime}");
        }
        
        // Verify critical services
        var criticalServices = new[]
        {
            typeof(IConfiguration),
            typeof(ILogger<>),
            typeof(IServiceProvider)
        };
        
        foreach (var serviceType in criticalServices)
        {
            var service = options.ServiceProvider.GetService(serviceType);
            if (service == null)
            {
                options.Logger.LogWarning($"Critical service not registered: {serviceType.Name}");
            }
        }
    }
}
```

## Multi-Project Solutions with Source Generation

When using source generation with solutions containing many plugin projects, each project with internal types needs its own `[GenerateTypeRegistry]` attribute. For large solutions, you can use MSBuild conventions to reduce boilerplate.

### Using Directory.Build.props

Create a `Directory.Build.props` file at your solution root to automatically generate a source file with the attribute for projects matching a naming convention:

```xml
<!-- Directory.Build.props -->
<Project>

  <!-- Enable auto-generation for projects matching naming patterns -->
  <PropertyGroup>
    <NeedlrAutoGenerate Condition="$(MSBuildProjectName.EndsWith('.Plugin'))">true</NeedlrAutoGenerate>
    <NeedlrAutoGenerate Condition="$(MSBuildProjectName.EndsWith('.Plugins'))">true</NeedlrAutoGenerate>
    <NeedlrAutoGenerate Condition="$(MSBuildProjectName.EndsWith('Plugin'))">true</NeedlrAutoGenerate>
  </PropertyGroup>

  <!-- Or match by prefix -->
  <PropertyGroup>
    <NeedlrAutoGenerate Condition="$(MSBuildProjectName.StartsWith('MyCompany.'))">true</NeedlrAutoGenerate>
  </PropertyGroup>

  <!-- Set namespace prefix to project name by default -->
  <PropertyGroup Condition="'$(NeedlrAutoGenerate)' == 'true'">
    <NeedlrNamespacePrefix Condition="'$(NeedlrNamespacePrefix)' == ''">$(MSBuildProjectName)</NeedlrNamespacePrefix>
  </PropertyGroup>

</Project>
```

Then create a `Directory.Build.targets` file to generate the attribute:

```xml
<!-- Directory.Build.targets -->
<Project>

  <Target Name="NeedlrGenerateTypeRegistryAttribute" 
          BeforeTargets="CoreCompile"
          Condition="'$(NeedlrAutoGenerate)' == 'true'">
    
    <PropertyGroup>
      <_NeedlrGeneratedFile>$(IntermediateOutputPath)NeedlrGeneratedTypeRegistry.g.cs</_NeedlrGeneratedFile>
    </PropertyGroup>
    
    <WriteLinesToFile
      File="$(_NeedlrGeneratedFile)"
      Lines="// Auto-generated by Directory.Build.targets;[assembly: NexusLabs.Needlr.Generators.GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { &quot;$(NeedlrNamespacePrefix)&quot; })]"
      Overwrite="true" />
    
    <ItemGroup>
      <Compile Include="$(_NeedlrGeneratedFile)" />
    </ItemGroup>
    
  </Target>

</Project>
```

### Solution Structure Example

```
MySolution/
├── Directory.Build.props           # Naming convention rules
├── Directory.Build.targets         # Auto-generates attribute
├── MyCompany.App/                  # Host - explicit [GenerateTypeRegistry]
│   └── GeneratorAssemblyInfo.cs    # Custom namespace prefixes
├── MyCompany.Auth.Plugin/          # Auto-generates ✓
├── MyCompany.Caching.Plugin/       # Auto-generates ✓
├── MyCompany.Logging.Plugin/       # Auto-generates ✓
└── ... more plugins                # All auto-generate ✓
```

### Opting Out Specific Projects

To exclude a specific project from auto-generation, add to its `.csproj`:

```xml
<PropertyGroup>
  <NeedlrAutoGenerate>false</NeedlrAutoGenerate>
</PropertyGroup>
```

### Custom Namespace Prefix Per Project

Override the default namespace prefix in a specific project:

```xml
<PropertyGroup>
  <NeedlrNamespacePrefix>MyCompany.CustomNamespace</NeedlrNamespacePrefix>
</PropertyGroup>
```

### Why This Matters

When plugin projects contain internal types, the host application's generator cannot access them. Each plugin must generate its own type registry. Without the MSBuild convention approach, you would need to manually add `[GenerateTypeRegistry]` to every plugin project.

The generator emits error `NDLRGEN002` if it detects internal plugin types in a referenced assembly without `[GenerateTypeRegistry]`, helping you identify projects that need the attribute.

## Assembly Loading Control

### Automatic Assembly Loading

When using source generation, Needlr automatically discovers all referenced assemblies that have `[GenerateTypeRegistry]` and ensures they are loaded at startup. This is critical because:


- **Module initializers only run when an assembly is loaded** - If your code never directly references a type from an assembly, that assembly never loads
- **Transitive dependencies** - Plugin assemblies referenced by your project but never directly used in code would be invisible to the type registry

Needlr solves this by generating a `ForceLoadReferencedAssemblies()` method that uses `typeof()` to force assembly loading:

```csharp
// Generated in NeedlrSourceGenBootstrap.g.cs
[MethodImpl(MethodImplOptions.NoInlining)]
private static void ForceLoadReferencedAssemblies()
{
    _ = typeof(global::MyApp.Features.Logging.Generated.TypeRegistry).Assembly;
    _ = typeof(global::MyApp.Features.Scheduling.Generated.TypeRegistry).Assembly;
    // ... all discovered assemblies with [GenerateTypeRegistry]
}
```

This is fully AOT-compatible - `typeof()` is resolved at compile time.

### Controlling Assembly Load Order with [NeedlrAssemblyOrder]

By default, referenced assemblies are loaded in alphabetical order. If you need specific assemblies to load before or after others (e.g., when plugins have dependencies on other plugins being registered first), use the `[NeedlrAssemblyOrder]` attribute:

```csharp
using NexusLabs.Needlr.Generators;

// In your host application's assembly attributes
[assembly: GenerateTypeRegistry]
[assembly: NeedlrAssemblyOrder(
    First = new[] { "MyApp.Features.Logging", "MyApp.Features.Configuration" },
    Last = new[] { "MyApp.Features.Health" })]
```

**How ordering works:**
1. Assemblies in `First` are loaded first, in the order specified
2. All other discovered assemblies are loaded alphabetically
3. Assemblies in `Last` are loaded last, in the order specified

**Example scenario:** Your `AuthenticationPlugin` needs `ILogger` which is registered by `LoggingPlugin`:

```csharp
[assembly: GenerateTypeRegistry]
[assembly: NeedlrAssemblyOrder(
    First = new[] { "MyApp.Features.Logging" })]  // Logging loads first

namespace MyApp.Bootstrap;

public class Startup
{
    // AuthenticationPlugin can now safely depend on ILogger being registered
}
```

### When You Don't Need Assembly Order

You typically don't need `[NeedlrAssemblyOrder]` when:


- Plugins don't have inter-dependencies during registration
- You're using the default registration which handles most scenarios
- All plugin configuration happens at runtime (not during registration)

You DO need `[NeedlrAssemblyOrder]` when:


- A plugin's `Configure()` method calls `GetRequiredService<T>()` and `T` is registered by another plugin
- You have strict initialization order requirements
- Debugging issues where plugins fail because their dependencies aren't registered yet

## Working with Other Source Generators

When using Needlr with other source generators that modify your types (such as generators that add constructors to partial classes), you may encounter scenarios where Needlr's generator cannot see the constructor that will be added by another generator.

### The Problem

Source generators in .NET run in isolation - they cannot see each other's output. If you have a partial class like:

```csharp
// Your code - another generator will add a constructor
[CacheProvider("EngageFeed")]  // Triggers CacheProviderGenerator
public partial class EngageFeedCacheProvider { }

// CacheProviderGenerator produces:
public sealed partial class EngageFeedCacheProvider(ICacheProvider _cacheProvider) { }
```

Needlr's generator sees only your original declaration without the constructor, so it would generate an incorrect factory:

```csharp
// Wrong! Missing the ICacheProvider dependency
sp => new EngageFeedCacheProvider()
```

### Solution: The DeferToContainer Attribute

Use `[DeferToContainer]` to explicitly declare the constructor parameter types that another generator will add. Needlr will use these types to generate the correct factory:

```csharp
using NexusLabs.Needlr;

// Declare the expected constructor parameters
[DeferToContainer(typeof(ICacheProvider))]
[CacheProvider("EngageFeed")]
public partial class EngageFeedCacheProvider { }
```

Needlr now generates the correct factory:

```csharp
// Correct! Resolves ICacheProvider from the container
sp => new EngageFeedCacheProvider(
    sp.GetRequiredService<ICacheProvider>())
```

### Multiple Dependencies

You can declare multiple constructor parameters in order:

```csharp
[DeferToContainer(
    typeof(ICacheProvider), 
    typeof(ILogger<EngageFeedCacheProvider>),
    typeof(IOptions<CacheOptions>))]
[CacheProvider("EngageFeed")]
public partial class EngageFeedCacheProvider { }
```

This generates:

```csharp
sp => new EngageFeedCacheProvider(
    sp.GetRequiredService<ICacheProvider>(),
    sp.GetRequiredService<ILogger<EngageFeedCacheProvider>>(),
    sp.GetRequiredService<IOptions<CacheOptions>>())
```

### Parameterless Constructor Override

Use `[DeferToContainer]` without parameters if the other generator will add a parameterless constructor or you want to ensure Needlr doesn't inspect the actual constructors:

```csharp
[DeferToContainer]  // Empty - no constructor parameters
[SomeOtherGeneratorAttribute]
public partial class SimpleService { }
```

### Compile-Time Validation

If the declared parameter types don't match the actual generated constructor, the build will fail with a compile error. This provides compile-time safety - you'll know immediately if the other generator changes its output.

### ⚠️ Critical: The Attribute Must Be in Your Original Source

**The `[DeferToContainer]` attribute MUST be placed on your original partial class declaration - NOT in generated code.**

Source generators run in isolation and cannot see output from other generators. If another generator adds `[DeferToContainer]` to its generated output, Needlr's generator will **never see it**.

```csharp
// ❌ WRONG - Placing attribute in generated code doesn't work!
// CacheProviderGenerator.g.cs (GENERATED FILE)
[DeferToContainer(typeof(ICacheProvider))]  // Needlr can't see this!
public sealed partial class EngageFeedCacheProvider(ICacheProvider _cacheProvider) { }

// ✅ CORRECT - Place attribute in your original source file
// EngageFeedCacheProvider.cs (YOUR FILE)
[DeferToContainer(typeof(ICacheProvider))]  // Needlr sees this!
[CacheProvider("EngageFeed")]
public partial class EngageFeedCacheProvider { }
```

The analyzer `NDLRCOR003` will detect and report an error if it finds `[DeferToContainer]` in generated code. See the [NDLRCOR003 documentation](analyzers/NDLRCOR003.md) for more details.

### When to Use DeferToContainer

Use `[DeferToContainer]` when:

1. **Another source generator adds a constructor** to your partial class
2. **The constructor has dependencies** that need to be resolved from DI
3. **You're using source generation** (`.UsingSourceGen()`) - reflection-based discovery doesn't have this limitation

You do NOT need `[DeferToContainer]` when:

1. Using `.UsingReflection()` - it discovers constructors at runtime
2. Your class has an explicit constructor in your source code
3. The other generator doesn't add constructor parameters

### Example: FusionCache Integration

Here's a complete example integrating with a hypothetical `CacheProviderGenerator`:

```csharp
// In your cache providers project
namespace MyApp.Caching;

public interface ICacheProvider { }

// The CacheProviderGenerator will add:
// public sealed partial class EngageFeedCacheProvider(ICacheProvider _cacheProvider)

[DeferToContainer(typeof(ICacheProvider))]
[CacheProvider("EngageFeed")]
public partial class EngageFeedCacheProvider { }

[DeferToContainer(typeof(ICacheProvider), typeof(ILogger<UserProfileCacheProvider>))]
[CacheProvider("UserProfile")]
public partial class UserProfileCacheProvider { }
```

```csharp
// In your host application
var app = new Syringe()
    .UsingSourceGen()
    .ForWebApplication()
    .BuildWebApplication();

// Both cache providers are correctly registered with their dependencies resolved
```

## Debugging Service Registrations

Needlr provides diagnostic tools to help you understand and debug your service registrations.

### Dumping All Registrations

Use the `Dump()` extension method to get a formatted view of all registrations:

```csharp
using NexusLabs.Needlr;

var services = new ServiceCollection();
services.AddTransient<IMyService, MyService>();
services.AddSingleton<ICache, MemoryCache>();
services.AddScoped<IDbContext, AppDbContext>();

// Dump all registrations to console
Console.WriteLine(services.Dump());
```

Output:
```
═══ Service Registrations (3 registrations) ═══

┌─ ICache
│  Lifetime: Singleton
│  Implementation: MemoryCache
└─

┌─ IDbContext
│  Lifetime: Scoped
│  Implementation: AppDbContext
└─

┌─ IMyService
│  Lifetime: Transient
│  Implementation: MyService
└─
```

### Filtering and Grouping

Use `DumpOptions` to filter and organize the output:

```csharp
// Only show singletons
Console.WriteLine(services.Dump(new DumpOptions 
{ 
    LifetimeFilter = ServiceLifetime.Singleton 
}));

// Group by lifetime
Console.WriteLine(services.Dump(new DumpOptions 
{ 
    GroupByLifetime = true 
}));

// Filter by service type
Console.WriteLine(services.Dump(new DumpOptions 
{ 
    ServiceTypeFilter = t => t.Namespace?.Contains("MyApp") == true 
}));
```

### Detailed Registration Info

Get detailed information about a specific registration:

```csharp
var registrations = services.GetServiceRegistrations();
foreach (var reg in registrations)
{
    Console.WriteLine(reg.ToDetailedString());
}
```

### Dumping from ServiceProvider

You can also dump from a built service provider (requires `IServiceCollection` to be registered):

```csharp
var provider = new Syringe()
    .UsingSourceGen()
    .BuildServiceProvider();

// Needlr automatically registers IServiceCollection, so Dump() works
Console.WriteLine(provider.Dump());
```

## Container Verification

Needlr provides verification APIs to detect common configuration issues at startup.

### Detecting Lifetime Mismatches

A lifetime mismatch (also called "captive dependency") occurs when a longer-lived service depends on a shorter-lived service. For example, a Singleton that depends on a Scoped service will "capture" that scoped instance, causing it to live for the entire application lifetime instead of the intended scope.

```csharp
using NexusLabs.Needlr;

var services = new ServiceCollection();
services.AddScoped<IDbContext, AppDbContext>();        // Scoped
services.AddSingleton<ICacheService, CacheService>();  // Singleton depends on IDbContext

// Detect mismatches
var mismatches = services.DetectLifetimeMismatches();

foreach (var mismatch in mismatches)
{
    Console.WriteLine(mismatch.ToDetailedString());
}
```

Output:
```
┌─ Lifetime Mismatch
│  ICacheService (Singleton)
│    └─ depends on ─▶ IDbContext (Scoped)
│
│  Problem: Singleton service will capture Scoped dependency
│  Fix: Change ICacheService to Scoped,
│       or change IDbContext to Singleton,
│       or inject IServiceScopeFactory instead.
└─
```

### Lifetime Hierarchy

From longest to shortest lifetime:
1. **Singleton** - Lives for entire application lifetime
2. **Scoped** - Lives for the scope/request lifetime  
3. **Transient** - New instance every time

A mismatch is detected when a service depends on another with a shorter lifetime:

- ❌ Singleton → Scoped (mismatch)
- ❌ Singleton → Transient (mismatch)
- ❌ Scoped → Transient (mismatch)
- ✅ Scoped → Singleton (ok)
- ✅ Transient → anything (ok)

### Automatic Verification with Verify()

Use the `Verify()` extension method to automatically detect and handle issues:

```csharp
// Throws ContainerVerificationException on issues (strict mode)
services.Verify(VerificationOptions.Strict);

// Warns to console but doesn't throw (default behavior)
services.Verify();

// Disable verification (silent mode)
services.Verify(VerificationOptions.Disabled);

// Custom configuration
services.Verify(new VerificationOptions
{
    LifetimeMismatchBehavior = VerificationBehavior.Throw,
    CircularDependencyBehavior = VerificationBehavior.Warn,
    IssueReporter = issue => logger.LogWarning(issue.Message)
});
```

### Getting Detailed Diagnostics

Use `VerifyWithDiagnostics()` to get a result object instead of throwing:

```csharp
var result = services.VerifyWithDiagnostics();

if (!result.IsValid)
{
    Console.WriteLine(result.ToDetailedReport());
    // ❌ Container verification found 2 issue(s):
    // 
    // [LifetimeMismatch] Lifetime mismatch: ICacheService (Singleton) depends on IDbContext (Scoped)
    // ...
}

// Or throw if needed
result.ThrowIfInvalid();
```

### Compile-Time Detection

Needlr includes Roslyn analyzers that detect issues at compile time:


- **[NDLRCOR005](analyzers/NDLRCOR005.md)**: Lifetime mismatch warnings
- **[NDLRCOR006](analyzers/NDLRCOR006.md)**: Circular dependency errors

These analyzers work with Needlr's registration attributes (`[Singleton]`, `[Scoped]`, `[Transient]`, `[RegisterAs]`).