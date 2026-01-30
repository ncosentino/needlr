# NDLRGEN015: Validator type mismatch

## Cause

A validator's generic type parameter doesn't match the options type it's applied to.

## Rule Description

When using `[Options(Validator = typeof(SomeValidator))]`, the validator must be designed to validate the same type as the options class. If you have `IOptionsValidator<DatabaseOptions>` but apply it to `CacheOptions`, this error is raised.

## How to Fix

Ensure the validator is for the correct options type:

```csharp
// ❌ Wrong - validator is for DatabaseOptions, not CacheOptions
[Options(Validator = typeof(DatabaseOptionsValidator), ValidateOnStart = true)]
public class CacheOptions  // Mismatch!
{
    public int TimeoutSeconds { get; set; }
}

public class DatabaseOptionsValidator : IOptionsValidator<DatabaseOptions>
{
    public IEnumerable<ValidationError> Validate(DatabaseOptions options) { ... }
}

// ✅ Correct - validator matches options type
[Options(Validator = typeof(CacheOptionsValidator), ValidateOnStart = true)]
public class CacheOptions
{
    public int TimeoutSeconds { get; set; }
}

public class CacheOptionsValidator : IOptionsValidator<CacheOptions>
{
    public IEnumerable<ValidationError> Validate(CacheOptions options) { ... }
}
```

## Example

### Code with Error

```csharp
using NexusLabs.Needlr.Generators;

// NDLRGEN015: Validator 'PaymentValidator' validates 'PaymentOptions' 
//             but is applied to options type 'ShippingOptions'
[Options(Validator = typeof(PaymentValidator), ValidateOnStart = true)]
public class ShippingOptions
{
    public string Carrier { get; set; } = "";
}

public class PaymentValidator : IOptionsValidator<PaymentOptions>
{
    public IEnumerable<ValidationError> Validate(PaymentOptions options)
    {
        // This validates PaymentOptions, not ShippingOptions!
        yield break;
    }
}
```

### Fixed Code

```csharp
using NexusLabs.Needlr.Generators;

[Options(Validator = typeof(ShippingValidator), ValidateOnStart = true)]
public class ShippingOptions
{
    public string Carrier { get; set; } = "";
}

// ✅ Validator now matches the options type
public class ShippingValidator : IOptionsValidator<ShippingOptions>
{
    public IEnumerable<ValidationError> Validate(ShippingOptions options)
    {
        if (string.IsNullOrEmpty(options.Carrier))
            yield return "Carrier is required";
    }
}
```

## Common Causes

1. **Copy-paste error** - Copied validation setup from another options class
2. **Refactoring** - Renamed options class but forgot to update validator
3. **Wrong validator** - Selected wrong validator from IntelliSense

## See Also

- [NDLRGEN014](NDLRGEN014.md) - Validator type has no validation method
- [NDLRGEN016](NDLRGEN016.md) - Validation method not found
- [Options Documentation](../options.md)
