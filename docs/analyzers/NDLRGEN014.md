# NDLRGEN014: Validator type has no validation method

## Cause

A type specified in `[Options(Validator = typeof(...))]` does not have a valid validation method.

## Rule Description

When you specify a `Validator` type on the `[Options]` attribute, that type must have a way to validate the options. The validator must either:

1. Implement `IOptionsValidator<T>` (from `NexusLabs.Needlr.Generators`)
2. Have a `Validate(TOptions)` method that returns `IEnumerable<ValidationError>` or `IEnumerable<string>`

## How to Fix

Add a validation method to your validator type:

```csharp
// ❌ Wrong - no validation method
public class MyOptionsValidator
{
    // Missing Validate method
}

// ✅ Option 1: Implement IOptionsValidator<T>
public class MyOptionsValidator : IOptionsValidator<MyOptions>
{
    public IEnumerable<ValidationError> Validate(MyOptions options)
    {
        if (string.IsNullOrEmpty(options.ApiKey))
            yield return "ApiKey is required";
    }
}

// ✅ Option 2: Add a Validate method with correct signature
public class MyOptionsValidator
{
    public IEnumerable<string> Validate(MyOptions options)
    {
        if (string.IsNullOrEmpty(options.ApiKey))
            yield return "ApiKey is required";
    }
}
```

## Example

### Code with Error

```csharp
using NexusLabs.Needlr.Generators;

[Options(Validator = typeof(StripeOptionsValidator), ValidateOnStart = true)]
public class StripeOptions
{
    public string ApiKey { get; set; } = "";
}

// NDLRGEN014: Validator type 'StripeOptionsValidator' must have a Validate method
public class StripeOptionsValidator
{
    // No validation method!
}
```

### Fixed Code

```csharp
using NexusLabs.Needlr.Generators;

[Options(Validator = typeof(StripeOptionsValidator), ValidateOnStart = true)]
public class StripeOptions
{
    public string ApiKey { get; set; } = "";
}

public class StripeOptionsValidator : IOptionsValidator<StripeOptions>
{
    public IEnumerable<ValidationError> Validate(StripeOptions options)
    {
        if (!options.ApiKey.StartsWith("sk_"))
            yield return "ApiKey must start with 'sk_'";
    }
}
```

## Valid Validation Method Signatures

The following signatures are recognized:

```csharp
// Instance method on external validator
IEnumerable<ValidationError> Validate(TOptions options)
IEnumerable<string> Validate(TOptions options)

// Static method on external validator
static IEnumerable<ValidationError> Validate(TOptions options)
static IEnumerable<string> Validate(TOptions options)
```

## See Also

- [NDLRGEN015](NDLRGEN015.md) - Validator type mismatch
- [NDLRGEN016](NDLRGEN016.md) - Validation method not found
- [NDLRGEN017](NDLRGEN017.md) - Validation method has wrong signature
- [Options Documentation](../options.md)
