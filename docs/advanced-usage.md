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

### Nested Decorators

Apply multiple decorators in sequence:

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
        .UseLibTestEntrySorting()
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