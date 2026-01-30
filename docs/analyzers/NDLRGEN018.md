# NDLRGEN018: Validator won't run

## Cause

A `Validator` is specified but `ValidateOnStart` is `false`, so the validator will never be invoked.

## Rule Description

When you specify `[Options(Validator = typeof(...))]` but don't set `ValidateOnStart = true`, the validator is registered but never actually called. This is likely a configuration mistake.

This is a **warning** because the code is technically valid, but probably not what you intended.

## How to Fix

Either enable validation or remove the validator:

```csharp
// ⚠️ Warning - validator specified but won't run
[Options(Validator = typeof(MyValidator))]  // ValidateOnStart defaults to false
public class MyOptions { }

// ✅ Option 1: Enable ValidateOnStart
[Options(Validator = typeof(MyValidator), ValidateOnStart = true)]
public class MyOptions { }

// ✅ Option 2: Remove the unused Validator
[Options]
public class MyOptions { }
```

## Example

### Code with Warning

```csharp
using NexusLabs.Needlr.Generators;

// NDLRGEN018: Validator 'PaymentOptionsValidator' will not run 
//             because ValidateOnStart is false
[Options(Validator = typeof(PaymentOptionsValidator))]
public class PaymentOptions
{
    public string MerchantId { get; set; } = "";
}

public class PaymentOptionsValidator : IOptionsValidator<PaymentOptions>
{
    public IEnumerable<ValidationError> Validate(PaymentOptions options)
    {
        if (string.IsNullOrEmpty(options.MerchantId))
            yield return "MerchantId is required";
    }
}
```

### Fixed Code

```csharp
using NexusLabs.Needlr.Generators;

// ✅ ValidateOnStart = true enables the validator
[Options(Validator = typeof(PaymentOptionsValidator), ValidateOnStart = true)]
public class PaymentOptions
{
    public string MerchantId { get; set; } = "";
}

public class PaymentOptionsValidator : IOptionsValidator<PaymentOptions>
{
    public IEnumerable<ValidationError> Validate(PaymentOptions options)
    {
        if (string.IsNullOrEmpty(options.MerchantId))
            yield return "MerchantId is required";
    }
}
```

## Why This Warning Exists

Specifying a validator without enabling validation is almost always a mistake:

- You wrote validation logic that will never execute
- Your application won't fail fast on invalid configuration
- Future maintainers may assume validation is happening

## Suppressing the Warning

If you intentionally want to specify a validator without running it at startup (e.g., for manual validation later), suppress the warning:

```csharp
#pragma warning disable NDLRGEN018
[Options(Validator = typeof(MyValidator))]
public class MyOptions { }
#pragma warning restore NDLRGEN018
```

Or in `.editorconfig`:

```ini
[*.cs]
dotnet_diagnostic.NDLRGEN018.severity = none
```

## See Also

- [NDLRGEN019](NDLRGEN019.md) - Validation method won't run
- [NDLRGEN014](NDLRGEN014.md) - Validator type has no validation method
- [Options Documentation](../options.md)
