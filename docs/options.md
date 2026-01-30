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

See [FluentValidation Integration](./fluentvalidation.md) for more details.

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

## Best Practices

1. **Always validate in production**: Use `ValidateOnStart = true` to fail fast on misconfiguration

2. **Use strongly-typed sections**: Prefer explicit section names over relying on conventions when your config structure is complex

3. **Separate validation logic**: Use external validators for complex validation rules to keep options classes clean

4. **Document required settings**: Use `[Required]` and XML comments to make configuration requirements clear

5. **Provide sensible defaults**: Initialize properties with default values for optional settings

## See Also

- [FluentValidation Integration](./fluentvalidation.md)
- [Getting Started](./getting-started.md)
- [Analyzers Reference](./analyzers/README.md)
