# API Reference

## NexusLabs.Needlr

### Attributes

#### DoNotAutoRegisterAttribute

Prevents a type from being automatically registered by Needlr's discovery mechanism.

```csharp
[DoNotAutoRegister]
public class ManualService { }
```

#### DoNotInjectAttribute

Indicates that a type should not be injected as a dependency.

```csharp
[DoNotInject]
public interface IInternalOnly { }
```

### Interfaces

#### IServiceCollectionPlugin

Allows configuration of the service collection during registration.

```csharp
public interface IServiceCollectionPlugin
{
    void Configure(ServiceCollectionPluginOptions options);
}
```

**ServiceCollectionPluginOptions Properties:**
- `IServiceCollection Services` - The service collection to configure
- `ILogger Logger` - Logger for diagnostic output
- `IConfiguration Configuration` - Application configuration

#### IPostBuildServiceCollectionPlugin

Executes after the service provider has been built.

```csharp
public interface IPostBuildServiceCollectionPlugin
{
    void Configure(PostBuildServiceCollectionPluginOptions options);
}
```

**PostBuildServiceCollectionPluginOptions Properties:**
- `IServiceCollection Services` - The built service collection
- `IServiceProvider ServiceProvider` - The built service provider
- `ILogger Logger` - Logger for diagnostic output

#### IPluginFactory

Factory for creating plugin instances.

```csharp
public interface IPluginFactory
{
    TPlugin CreatePlugin<TPlugin>() where TPlugin : class, new();
}
```

## NexusLabs.Needlr.Injection

### Core Classes

#### Syringe

The main configuration class for dependency injection.

```csharp
public sealed record Syringe
{
    // Build service provider with default configuration
    public IServiceProvider BuildServiceProvider();
    
    // Build service provider with custom configuration
    public IServiceProvider BuildServiceProvider(IConfiguration config);
    
    // Get or create components
    public ITypeRegistrar GetOrCreateTypeRegistrar();
    public ITypeFilterer GetOrCreateTypeFilterer();
    public IServiceCollectionPopulator GetOrCreateServiceCollectionPopulator(
        ITypeRegistrar typeRegistrar, 
        ITypeFilterer typeFilterer);
    public IAssemblyProvider GetOrCreateAssemblyProvider();
    public IReadOnlyList<Assembly> GetAdditionalAssemblies();
    public IReadOnlyList<Action<IServiceCollection>> GetPostPluginRegistrationCallbacks();
}
```

### Extension Methods

#### SyringeExtensions

Fluent configuration methods for Syringe.

```csharp
// Type registrar configuration
public static Syringe UsingTypeRegistrar(this Syringe syringe, ITypeRegistrar typeRegistrar);
public static Syringe UsingDefaultTypeRegistrar(this Syringe syringe);

// Type filterer configuration
public static Syringe UsingTypeFilterer(this Syringe syringe, ITypeFilterer typeFilterer);
public static Syringe UsingDefaultTypeFilterer(this Syringe syringe);

// Assembly provider configuration
public static Syringe UsingAssemblyProvider(
    this Syringe syringe, 
    IAssemblyProvider assemblyProvider);
public static Syringe UsingAssemblyProvider(
    this Syringe syringe, 
    Func<IAssembyProviderBuilder, IAssemblyProvider> builderFunc);

// Additional assemblies
public static Syringe UsingAdditionalAssemblies(
    this Syringe syringe, 
    params Assembly[] additionalAssemblies);

// Post-registration callbacks
public static Syringe UsingPostPluginRegistrationCallback(
    this Syringe syringe, 
    Action<IServiceCollection> callback);

// Decorator support
public static Syringe AddDecorator<TService, TDecorator>(this Syringe syringe)
    where TService : class
    where TDecorator : class, TService;
```

### Interfaces

#### ITypeRegistrar

Handles registration of discovered types.

```csharp
public interface ITypeRegistrar
{
    void RegisterTypes(
        IServiceCollection services, 
        IEnumerable<Type> types, 
        ILogger logger);
}
```

#### ITypeFilterer

Filters types before registration.

```csharp
public interface ITypeFilterer
{
    IEnumerable<Type> Filter(IEnumerable<Type> types);
}
```

#### IAssemblyProvider

Provides assemblies for scanning.

```csharp
public interface IAssemblyProvider
{
    IEnumerable<Assembly> GetAssemblies();
}
```

#### IAssemblyLoader

Loads assemblies for scanning.

```csharp
public interface IAssemblyLoader
{
    IEnumerable<Assembly> LoadAssemblies();
}
```

#### IAssemblySorter

Sorts assemblies for processing order.

```csharp
public interface IAssemblySorter
{
    IEnumerable<Assembly> Sort(IEnumerable<Assembly> assemblies);
}
```

#### IServiceCollectionPopulator

Populates the service collection with discovered types.

```csharp
public interface IServiceCollectionPopulator
{
    void Populate(
        IServiceCollection services,
        IEnumerable<Assembly> assemblies,
        ILogger logger,
        IConfiguration configuration);
}
```

#### IServiceProviderBuilder

Builds the service provider.

```csharp
public interface IServiceProviderBuilder
{
    IServiceProvider Build(
        IServiceCollection services,
        IConfiguration config,
        IEnumerable<Action<IServiceCollection>> postPluginRegistrationCallbacks);
}
```

### Assembly Provider Builder

#### IAssembyProviderBuilder

Fluent builder for assembly providers.

```csharp
public interface IAssembyProviderBuilder
{
    IAssembyProviderBuilder MatchingAssemblies(Func<string, bool> predicate);
    IAssembyProviderBuilder UseDefaultSorting();
    IAssembyProviderBuilder UseAlphabeticalSorting();
    IAssembyProviderBuilder UseLibTestEntrySorting();
    IAssemblyProvider Build();
}
```

## NexusLabs.Needlr.AspNet

### Core Classes

#### WebApplicationSyringe

Extension of Syringe for web applications.

```csharp
public sealed record WebApplicationSyringe
{
    public WebApplication BuildWebApplication();
    public Task<WebApplication> BuildWebApplicationAsync();
}
```

### Extension Methods

#### SyringeAspNetExtensions

```csharp
// Convert to web application configuration
public static WebApplicationSyringe ForWebApplication(this Syringe syringe);
```

#### WebApplicationSyringeExtensions

```csharp
// Configure web application options
public static WebApplicationSyringe UsingOptions(
    this WebApplicationSyringe syringe,
    Func<CreateWebApplicationOptions> optionsFactory);

// Use custom web application factory
public static WebApplicationSyringe UsingWebApplicationFactory<TFactory>(
    this WebApplicationSyringe syringe)
    where TFactory : IWebApplicationFactory, new();

// Configure the WebApplicationBuilder before building
public static WebApplicationSyringe UsingConfigurationCallback(
    this WebApplicationSyringe syringe,
    Action<WebApplicationBuilder, CreateWebApplicationOptions> configureCallback);
```

### Classes

#### CreateWebApplicationOptions

Configuration for web application creation.

```csharp
public sealed record CreateWebApplicationOptions
{
    public static CreateWebApplicationOptions Default { get; }
    
    public string? ApplicationName { get; init; }
    public string? EnvironmentName { get; init; }
    public string? ContentRoot { get; init; }
    public string? WebRoot { get; init; }
    public Action<ILoggingBuilder>? ConfigureLogging { get; init; }
    public Action<ConfigurationManager>? ConfigureAppConfiguration { get; init; }
    public Action<IWebHostBuilder>? ConfigureWebHost { get; init; }
}
```

#### CreateWebApplicationOptionsExtensions

```csharp
// Set application name
public static CreateWebApplicationOptions UsingApplicationName(
    this CreateWebApplicationOptions options, 
    string applicationName);

// Set environment
public static CreateWebApplicationOptions UsingEnvironment(
    this CreateWebApplicationOptions options, 
    string environmentName);

// Configure content root
public static CreateWebApplicationOptions UsingContentRoot(
    this CreateWebApplicationOptions options, 
    string contentRoot);

// Configure web root
public static CreateWebApplicationOptions UsingWebRoot(
    this CreateWebApplicationOptions options, 
    string webRoot);

// Add startup console logger
public static CreateWebApplicationOptions UsingStartupConsoleLogger(
    this CreateWebApplicationOptions options);
```

### Interfaces

#### IWebApplicationFactory

Factory for creating web applications.

```csharp
public interface IWebApplicationFactory
{
    WebApplication Create(
        CreateWebApplicationOptions options,
        Func<WebApplicationBuilder> createWebApplicationBuilderCallback);
}
```

#### IWebApplicationBuilderPlugin

Plugin for configuring WebApplicationBuilder.

```csharp
public interface IWebApplicationBuilderPlugin
{
    void Configure(WebApplicationBuilderPluginOptions options);
}
```

**WebApplicationBuilderPluginOptions Properties:**
- `WebApplicationBuilder Builder` - The web application builder
- `ILogger Logger` - Logger for diagnostic output

#### IWebApplicationPlugin

Plugin for configuring WebApplication.

```csharp
public interface IWebApplicationPlugin
{
    void Configure(WebApplicationPluginOptions options);
}
```

**WebApplicationPluginOptions Properties:**
- `WebApplication WebApplication` - The built web application
- `ILogger Logger` - Logger for diagnostic output

## NexusLabs.Needlr.Injection.Scrutor

### Extension Methods

#### SyringeScrutorExtensions

```csharp
// Use Scrutor for type registration
public static Syringe UsingScrutorTypeRegistrar(this Syringe syringe);
```

### Classes

#### ScrutorTypeRegistrar

Implementation of ITypeRegistrar using Scrutor library.

```csharp
public class ScrutorTypeRegistrar : ITypeRegistrar
{
    public void RegisterTypes(
        IServiceCollection services,
        IEnumerable<Type> types,
        ILogger logger);
}
```

## NexusLabs.Needlr.Extensions.Configuration

### Extension Methods

#### SyringeExtensions

```csharp
// Add IConfiguration support
public static Syringe UsingConfiguration(this Syringe syringe);

// Add IConfiguration with custom setup
public static Syringe UsingConfiguration(
    this Syringe syringe,
    Action<IConfigurationBuilder> configureBuilder);
```

## NexusLabs.Needlr.Extensions.Logging

### Extension Methods

#### PostBuildServiceCollectionPluginOptionsExtensions

```csharp
// Configure logging
public static PostBuildServiceCollectionPluginOptions ConfigureLogging(
    this PostBuildServiceCollectionPluginOptions options,
    Action<ILoggingBuilder> configure);
```

## NexusLabs.Needlr.Carter

### Classes

#### CarterWebApplicationBuilderPlugin

Plugin for configuring Carter framework.

```csharp
public class CarterWebApplicationBuilderPlugin : IWebApplicationBuilderPlugin
{
    public void Configure(WebApplicationBuilderPluginOptions options);
}
```

#### CarterWebApplicationPlugin

Plugin for mapping Carter modules.

```csharp
public class CarterWebApplicationPlugin : IWebApplicationPlugin
{
    public void Configure(WebApplicationPluginOptions options);
}
```

## NexusLabs.Needlr.SignalR

### Interfaces

#### IHubRegistrationPlugin

Plugin for registering SignalR hubs.

```csharp
public interface IHubRegistrationPlugin
{
    void ConfigureHubs(IEndpointRouteBuilder endpoints);
}
```

### Classes

#### SignalRWebApplicationBuilderPlugin

Plugin for configuring SignalR services.

```csharp
public class SignalRWebApplicationBuilderPlugin : IWebApplicationBuilderPlugin
{
    public void Configure(WebApplicationBuilderPluginOptions options);
}
```

#### SignalRHubRegistrationPlugin

Base class for hub registration plugins.

```csharp
public abstract class SignalRHubRegistrationPlugin : IHubRegistrationPlugin, IWebApplicationPlugin
{
    public abstract void ConfigureHubs(IEndpointRouteBuilder endpoints);
    public void Configure(WebApplicationPluginOptions options);
}
```

## Common Patterns

### Basic Service Provider

```csharp
var serviceProvider = new Syringe().BuildServiceProvider();
```

### Configured Service Provider

```csharp
var serviceProvider = new Syringe()
    .UsingScrutorTypeRegistrar()
    .UsingAssemblyProvider(builder => builder
        .MatchingAssemblies(x => x.Contains("MyApp"))
        .UseLibTestEntrySorting()
        .Build())
    .UsingConfiguration()
    .BuildServiceProvider();
```

### Web Application

```csharp
var webApp = new Syringe()
    .ForWebApplication()
    .UsingOptions(() => CreateWebApplicationOptions.Default
        .UsingEnvironment("Development")
        .UsingStartupConsoleLogger())
    .BuildWebApplication();

await webApp.RunAsync();
```

### With Decorators

```csharp
var serviceProvider = new Syringe()
    .UsingPostPluginRegistrationCallback(services =>
    {
        services.AddSingleton<IService, ServiceImpl>();
    })
    .AddDecorator<IService, LoggingDecorator>()
    .AddDecorator<IService, CachingDecorator>()
    .BuildServiceProvider();
```