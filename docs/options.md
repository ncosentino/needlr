# Options Binding

The `[Options]` attribute provides automatic configuration binding for your options classes, integrating with Microsoft's Options pattern.

## Quick Start

```csharp
using NexusLabs.Needlr.Generators;

[Options]
public class CacheSettings
{
    public int TimeoutSeconds { get; set; } = 300;
    public int MaxItems { get; set; } = 1000;
}
```

```json
// appsettings.json
{
  "CacheSettings": {
    "TimeoutSeconds": 600,
    "MaxItems": 5000
  }
}
```

The generator automatically:
1. Binds your options class to the configuration section
2. Registers it with the DI container
3. Makes it available via `IOptions<CacheSettings>`, `IOptionsSnapshot<CacheSettings>`, or `IOptionsMonitor<CacheSettings>`

!!! warning "Configuration must be passed explicitly"

    The generated options binding reads from the `IConfiguration` registered in DI. If you
    use the **parameterless** `BuildServiceProvider()` overload, it registers an **empty**
    configuration — your `appsettings.json` values will be silently ignored and only the
    class's default property values will apply.

    **Always pass an `IConfiguration` when using `[Options]`:**

    ```csharp
    var config = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: true)
        .Build();

    var provider = new Syringe()
        .UsingSourceGen()
        .BuildServiceProvider(config); // ← pass config explicitly
    ```

    Web applications using `ForWebApplication()` or host-based apps using `ForHost()` handle
    this automatically — the host builder loads `appsettings.json` as part of its default
    configuration pipeline.

## Section Name

### Convention-Based (Default)

By default, the section name is inferred from the class name:

| Class Name | Section Name |
|------------|--------------|
| `CacheSettings` | `CacheSettings` |
| `DatabaseOptions` | `DatabaseOptions` |
| `SmtpConfiguration` | `SmtpConfiguration` |

### Explicit Section Name

Specify a custom section name:

```csharp
[Options("MyApp:Database")]
public class DatabaseOptions
{
    public string ConnectionString { get; set; } = "";
}
```

```json
{
  "MyApp": {
    "Database": {
      "ConnectionString": "Server=..."
    }
  }
}
```

## Named Options

For multiple configurations of the same type, use named options:

```csharp
[Options("Databases:Primary", Name = "Primary")]
public class DatabaseOptions
{
    public string ConnectionString { get; set; } = "";
}

[Options("Databases:Replica", Name = "Replica")]
public class ReplicaDatabaseOptions : DatabaseOptions { }
```

Retrieve named options using `IOptionsSnapshot<T>` or `IOptionsMonitor<T>`:

```csharp
public class MyService
{
    public MyService(IOptionsSnapshot<DatabaseOptions> options)
    {
        var primary = options.Get("Primary");
        var replica = options.Get("Replica");
    }
}
```

## Validation

### Data Annotations

Use standard `System.ComponentModel.DataAnnotations` attributes:

```csharp
using System.ComponentModel.DataAnnotations;

[Options(ValidateOnStart = true)]
public class ApiSettings
{
    [Required]
    public string ApiKey { get; set; } = "";
    
    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;
    
    [Url]
    public string BaseUrl { get; set; } = "";
}
```

When `ValidateOnStart = true`, the application will fail to start if validation fails.

### Custom Validation Method

Add a `Validate()` method to your options class:

```csharp
[Options(ValidateOnStart = true)]
public class StripeOptions
{
    public string ApiKey { get; set; } = "";
    public bool IsProduction { get; set; }
    
    // Convention: method named "Validate" is auto-discovered
    public IEnumerable<ValidationError> Validate()
    {
        if (IsProduction && !ApiKey.StartsWith("sk_live_"))
            yield return "Production requires a live API key";
        
        if (!IsProduction && !ApiKey.StartsWith("sk_test_"))
            yield return "Test mode requires a test API key";
    }
}
```

### Custom Method Name

Use a different method name with `ValidateMethod`:

```csharp
[Options(ValidateOnStart = true, ValidateMethod = nameof(CheckConfiguration))]
public class EmailOptions
{
    public string SmtpHost { get; set; } = "";
    
    public IEnumerable<ValidationError> CheckConfiguration()
    {
        if (string.IsNullOrEmpty(SmtpHost))
            yield return "SMTP host is required";
    }
}
```

### External Validator

Delegate validation to a separate class:

```csharp
[Options(ValidateOnStart = true, Validator = typeof(PaymentOptionsValidator))]
public class PaymentOptions
{
    public string MerchantId { get; set; } = "";
    public decimal MinAmount { get; set; }
    public decimal MaxAmount { get; set; }
}

public class PaymentOptionsValidator : IOptionsValidator<PaymentOptions>
{
    public IEnumerable<ValidationError> Validate(PaymentOptions options)
    {
        if (string.IsNullOrEmpty(options.MerchantId))
            yield return "MerchantId is required";
        
        if (options.MinAmount >= options.MaxAmount)
            yield return "MinAmount must be less than MaxAmount";
    }
}
```

### FluentValidation Integration

If you're using FluentValidation, validators are automatically discovered:

```csharp
[Options("Database", ValidateOnStart = true, Validator = typeof(DatabaseOptionsValidator))]
public class DatabaseOptions
{
    public string ConnectionString { get; set; } = "";
    public int MaxPoolSize { get; set; } = 100;
}

public class DatabaseOptionsValidator : AbstractValidator<DatabaseOptions>
{
    public DatabaseOptionsValidator()
    {
        RuleFor(x => x.ConnectionString)
            .NotEmpty()
            .WithMessage("Connection string is required");
        
        RuleFor(x => x.MaxPoolSize)
            .InclusiveBetween(1, 1000);
    }
}
```

For more details on FluentValidation integration, see the [Advanced Usage](./advanced-usage.md) guide.

## Attribute Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `SectionName` | `string?` | Class name | Configuration section to bind |
| `Name` | `string?` | `null` | Named options identifier |
| `ValidateOnStart` | `bool` | `false` | Validate at application startup |
| `ValidateMethod` | `string?` | `"Validate"` | Custom validation method name |
| `Validator` | `Type?` | `null` | External validator type |

## Generated Code

The `[Options]` attribute causes the generator to emit registration code in `TypeRegistry.RegisterOptions()`:

```csharp
// Generated code (simplified)
public static void RegisterOptions(IServiceCollection services, IConfiguration configuration)
{
    // Without validation
    services.Configure<CacheSettings>(configuration.GetSection("CacheSettings"));
    
    // With validation
    services.AddOptions<ApiSettings>()
        .BindConfiguration("ApiSettings")
        .ValidateDataAnnotations()
        .ValidateOnStart();
}
```

This `RegisterOptions` method is wired into the DI container automatically
on both execution paths:

- **Console / non-web path** — `ConfiguredSyringe.BuildServiceProvider(IConfiguration)` calls
  it via `SourceGenRegistry.TryGetOptionsRegistrar` as a post-plugin registration
  callback, passing the `IConfiguration` instance supplied to `BuildServiceProvider`.
- **ASP.NET Core web path** — `WebApplicationSyringe.BuildWebApplication()` calls it
  through the same mechanism, resolving `IConfiguration` from `WebApplicationBuilder.Services`
  at callback invocation time so `IOptions<T>` binds against `builder.Configuration`.

You do **not** need to call `services.AddOptions<T>().BindConfiguration(...)` from
a plugin or `Program.cs` in either path — the `[Options]` attribute is the only
registration you need.

## Using `[Options]` on the ASP.NET Core Web Application Path

When building via `BuildWebApplication()`, the generator-emitted
`RegisterOptions` call runs automatically during the same post-plugin
registration phase used for user-registered callbacks. A minimal example:

```csharp
// WeatherOptions.cs
using System.ComponentModel.DataAnnotations;
using NexusLabs.Needlr.Generators;

[Options(ValidateOnStart = true)]
public sealed class WeatherOptions
{
    [Required]
    public string Summary { get; set; } = string.Empty;

    [Range(-100, 100)]
    public double TemperatureCelsius { get; set; }
}
```

```csharp
// WeatherProvider.cs
using Microsoft.Extensions.Options;

internal sealed class WeatherProvider(IOptions<WeatherOptions> _options)
{
    public object GetWeather() => new
    {
        _options.Value.Summary,
        _options.Value.TemperatureCelsius,
    };
}
```

```csharp
// Program.cs
using NexusLabs.Needlr.AspNet;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;

var webApplication = new Syringe()
    .UsingSourceGen()
    .BuildWebApplication();

await webApplication.RunAsync();
```

```json
// appsettings.Development.json
{
  "Weather": {
    "Summary": "Warm",
    "TemperatureCelsius": 20
  }
}
```

At runtime, `GET /weather` handlers that take `WeatherProvider` via DI
observe `Summary = "Warm"` and `TemperatureCelsius = 20.0`. The bound
values come from the generator-emitted
`AddOptions<WeatherOptions>().BindConfiguration("Weather")` call, not from
any manual wiring in your plugin or composition root.

`ValidateOnStart = true` fails the host immediately if `Summary` is missing
or out of range — the host refuses to start with
`OptionsValidationException`. This is the behavior you want in production.

A full runnable version of this scenario lives at
`src/Examples/SourceGen/MinimalWebApiSourceGen`, and the end-to-end
integration tests that lock this behavior in are at
`src/NexusLabs.Needlr.IntegrationTests/SourceGen/OptionsWebApplicationSourceGenTests.cs`.

## Consuming Options

Inject options using standard Microsoft.Extensions.Options patterns:

```csharp
public class MyService
{
    private readonly CacheSettings _settings;
    
    // Option 1: IOptions<T> - singleton, doesn't reload
    public MyService(IOptions<CacheSettings> options)
    {
        _settings = options.Value;
    }
    
    // Option 2: IOptionsSnapshot<T> - scoped, reloads per request
    public MyService(IOptionsSnapshot<CacheSettings> options)
    {
        _settings = options.Value;
    }
    
    // Option 3: IOptionsMonitor<T> - singleton, live reload
    public MyService(IOptionsMonitor<CacheSettings> options)
    {
        _settings = options.CurrentValue;
        options.OnChange(newValue => { /* handle reload */ });
    }
}
```

## Immutable Options

The `[Options]` attribute supports immutable options patterns, including `init`-only properties and records with init properties.

### Init-Only Properties

```csharp
[Options("Cache")]
public class CacheOptions
{
    public required string Host { get; init; }
    public int Port { get; init; } = 6379;
    public int TimeoutMs { get; init; } = 5000;
}
```

### Records with Init Properties

```csharp
[Options("Redis")]
public record RedisOptions
{
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 6379;
}
```

### Configuration Reload with Immutable Types

Immutable types work correctly with `IOptionsMonitor<T>` for configuration reload. When the configuration file changes:

1. A **new instance** is created with the updated values
2. `IOptionsMonitor<T>.CurrentValue` returns the new instance
3. `OnChange()` callbacks receive the new instance

```csharp
public class CacheService
{
    private readonly IOptionsMonitor<CacheOptions> _options;

    public CacheService(IOptionsMonitor<CacheOptions> options)
    {
        _options = options;
        
        // React to configuration changes
        options.OnChange(newOptions =>
        {
            // newOptions is a new immutable instance
            Console.WriteLine($"Cache timeout changed to {newOptions.TimeoutMs}ms");
        });
    }

    public CacheOptions CurrentConfig => _options.CurrentValue;
}
```

### Positional Records

Positional records (records with primary constructor parameters) are supported when declared as `partial`:

```csharp
// ✅ WORKS - partial positional record
[Options("Redis")]
public partial record RedisOptions(string Host, int Port);

// ✅ WORKS - init-only properties (no partial needed)
[Options("Redis")]
public record RedisOptions
{
    public string Host { get; init; } = "";
    public int Port { get; init; } = 6379;
}
```

When you use `partial`, Needlr generates a parameterless constructor that chains to the primary constructor:

```csharp
// Generated code:
public partial record RedisOptions
{
    public RedisOptions() : this(string.Empty, default) { }
}
```

This enables Microsoft's configuration binder to instantiate the record.

> ⚠️ **Non-partial positional records** will emit warning [NDLRGEN021](analyzers/NDLRGEN021.md) because they cannot work with configuration binding at runtime.

## AOT Compatibility

> ⚠️ **Important**: The `[Options]` attribute is **not compatible with Native AOT**.

The generated code uses `Configure<T>()` and `BindConfiguration()` which rely on reflection for property binding. These APIs have `[RequiresDynamicCode]` attributes, making them incompatible with AOT compilation.

If your project has `PublishAot=true` or `IsAotCompatible=true`, the generator will emit error [NDLRGEN020](analyzers/NDLRGEN020.md).

**Workaround for AOT projects**: Bind configuration manually:

```csharp
public class CacheSettings
{
    public int TimeoutSeconds { get; set; }
    
    public static CacheSettings FromConfiguration(IConfiguration config)
    {
        var section = config.GetSection("CacheSettings");
        return new CacheSettings
        {
            TimeoutSeconds = section.GetValue<int>("TimeoutSeconds")
        };
    }
}

// In startup:
var settings = CacheSettings.FromConfiguration(configuration);
services.AddSingleton(Microsoft.Extensions.Options.Options.Create(settings));
```

## Analyzers

The following analyzers help catch common mistakes:

| Analyzer | Severity | Description |
|----------|----------|-------------|
| [NDLRGEN014](analyzers/NDLRGEN014.md) | Error | Validator type has no validation method |
| [NDLRGEN015](analyzers/NDLRGEN015.md) | Error | Validator type mismatch |
| [NDLRGEN016](analyzers/NDLRGEN016.md) | Error | Validation method not found |
| [NDLRGEN017](analyzers/NDLRGEN017.md) | Error | Validation method has wrong signature |
| [NDLRGEN018](analyzers/NDLRGEN018.md) | Warning | Validator won't run (ValidateOnStart is false) |
| [NDLRGEN019](analyzers/NDLRGEN019.md) | Warning | Validation method won't run (ValidateOnStart is false) |
| [NDLRGEN020](analyzers/NDLRGEN020.md) | Error | [Options] is not compatible with Native AOT |
| [NDLRGEN021](analyzers/NDLRGEN021.md) | Warning | Positional record must be partial for [Options] |

## Best Practices

1. **Always validate in production**: Use `ValidateOnStart = true` to fail fast on misconfiguration

2. **Use strongly-typed sections**: Prefer explicit section names over relying on conventions when your config structure is complex

3. **Separate validation logic**: Use external validators for complex validation rules to keep options classes clean

4. **Document required settings**: Use `[Required]` and XML comments to make configuration requirements clear

5. **Provide sensible defaults**: Initialize properties with default values for optional settings

## See Also

- [Getting Started](./getting-started.md)
- [Analyzers Reference](./analyzers/README.md)
