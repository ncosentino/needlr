# Advanced Usage

This guide covers advanced scenarios and techniques for using Needlr in complex applications.

## Custom Type Registrars

### Implementing ITypeRegistrar

Create custom registration logic by implementing `ITypeRegistrar`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using NexusLabs.Needlr.Injection;

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

// Usage
var serviceProvider = new Syringe()
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

// Usage
var serviceProvider = new Syringe()
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

// Usage
var serviceProvider = new Syringe()
    .UsingTypeFilterer(new CompositeFilterer(
        new DefaultTypeFilterer(),
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

// Usage
var serviceProvider = new Syringe()
    .UsingAssemblyProvider(new PluginAssemblyProvider("./plugins"))
    .BuildServiceProvider();
```

## Advanced Web Application Configuration

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

## Multi-Tenant Applications

### Tenant-Specific Service Registration

```csharp
public interface ITenantService
{
    string TenantId { get; }
    void ProcessTenantData();
}

public class TenantServiceFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, Type> _tenantServices;
    
    public TenantServiceFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _tenantServices = new Dictionary<string, Type>
        {
            ["tenant-a"] = typeof(TenantAService),
            ["tenant-b"] = typeof(TenantBService),
            ["default"] = typeof(DefaultTenantService)
        };
    }
    
    public ITenantService GetTenantService(string tenantId)
    {
        var serviceType = _tenantServices.GetValueOrDefault(tenantId) 
            ?? _tenantServices["default"];
        
        return (ITenantService)_serviceProvider.GetRequiredService(serviceType);
    }
}

public class MultiTenantPlugin : IServiceCollectionPlugin
{
    public void Configure(ServiceCollectionPluginOptions options)
    {
        // Register all tenant services
        options.Services.AddScoped<TenantAService>();
        options.Services.AddScoped<TenantBService>();
        options.Services.AddScoped<DefaultTenantService>();
        
        // Register factory
        options.Services.AddSingleton<TenantServiceFactory>();
        
        // Register tenant resolver
        options.Services.AddScoped<ITenantService>(sp =>
        {
            var httpContext = sp.GetRequiredService<IHttpContextAccessor>().HttpContext;
            var tenantId = httpContext?.Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? "default";
            var factory = sp.GetRequiredService<TenantServiceFactory>();
            return factory.GetTenantService(tenantId);
        });
    }
}
```

## Performance Optimization

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

### Service Collection Optimization

```csharp
public class OptimizedRegistrationPlugin : IServiceCollectionPlugin
{
    public void Configure(ServiceCollectionPluginOptions options)
    {
        // Use TryAdd to avoid duplicate registrations
        options.Services.TryAddSingleton<ICommonService, CommonService>();
        
        // Use batch registration for related services
        var serviceTypes = new[]
        {
            typeof(ServiceA),
            typeof(ServiceB),
            typeof(ServiceC)
        };
        
        foreach (var type in serviceTypes)
        {
            options.Services.AddTransient(type);
        }
        
        // Use factory pattern for complex initialization
        options.Services.AddSingleton<IComplexService>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var logger = sp.GetRequiredService<ILogger<ComplexService>>();
            
            return new ComplexService(
                config.GetValue<string>("ComplexService:Setting1"),
                config.GetValue<int>("ComplexService:Setting2"),
                logger);
        });
    }
}
```

## Integration with External Systems

### gRPC Services

```csharp
public class GrpcPlugin : IWebApplicationBuilderPlugin, IWebApplicationPlugin
{
    public void Configure(WebApplicationBuilderPluginOptions options)
    {
        // Add gRPC services
        options.Builder.Services.AddGrpc();
        
        // Configure gRPC options
        options.Builder.Services.Configure<GrpcServiceOptions>(opts =>
        {
            opts.MaxReceiveMessageSize = 10 * 1024 * 1024; // 10MB
            opts.EnableDetailedErrors = options.Builder.Environment.IsDevelopment();
        });
    }
    
    public void Configure(WebApplicationPluginOptions options)
    {
        // Map gRPC services
        options.WebApplication.MapGrpcService<GreeterService>();
        options.WebApplication.MapGrpcService<DataService>();
        
        // Add gRPC-Web support
        options.WebApplication.UseGrpcWeb();
    }
}
```

### Message Queue Integration

```csharp
public class MessageQueuePlugin : IServiceCollectionPlugin, IPostBuildServiceCollectionPlugin
{
    public void Configure(ServiceCollectionPluginOptions options)
    {
        // Register message queue services
        options.Services.AddSingleton<IMessageQueueConnection>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            return new RabbitMQConnection(config.GetConnectionString("RabbitMQ"));
        });
        
        options.Services.AddSingleton<IMessagePublisher, RabbitMQPublisher>();
        options.Services.AddSingleton<IMessageConsumer, RabbitMQConsumer>();
        
        // Register message handlers
        options.Services.AddTransient<IMessageHandler<OrderMessage>, OrderMessageHandler>();
        options.Services.AddTransient<IMessageHandler<PaymentMessage>, PaymentMessageHandler>();
    }
    
    public void Configure(PostBuildServiceCollectionPluginOptions options)
    {
        // Start message consumers
        var consumer = options.ServiceProvider.GetRequiredService<IMessageConsumer>();
        consumer.StartConsuming();
        
        options.Logger.LogInformation("Message queue consumers started");
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