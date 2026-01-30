# NDLRGEN019: Validation method won't run

## Cause

A `ValidateMethod` is specified but `ValidateOnStart` is `false`, so the validation method will never be invoked.

## Rule Description

When you specify `[Options(ValidateMethod = "...")]` but don't set `ValidateOnStart = true`, the validation method exists but is never called. This is likely a configuration mistake.

This is a **warning** because the code is technically valid, but probably not what you intended.

## How to Fix

Either enable validation or remove the ValidateMethod:

```csharp
// ⚠️ Warning - method specified but won't run
[Options(ValidateMethod = "CheckConfig")]  // ValidateOnStart defaults to false
public class MyOptions
{
    public IEnumerable<ValidationError> CheckConfig() { ... }
}

// ✅ Option 1: Enable ValidateOnStart
[Options(ValidateMethod = "CheckConfig", ValidateOnStart = true)]
public class MyOptions
{
    public IEnumerable<ValidationError> CheckConfig() { ... }
}

// ✅ Option 2: Remove the unused ValidateMethod
[Options]
public class MyOptions
{
    // Method can stay but won't be auto-invoked
    public IEnumerable<ValidationError> CheckConfig() { ... }
}
```

## Example

### Code with Warning

```csharp
using NexusLabs.Needlr.Generators;

// NDLRGEN019: ValidateMethod 'CheckSettings' will not run 
//             because ValidateOnStart is false
[Options(ValidateMethod = "CheckSettings")]
public class FeatureFlags
{
    public bool EnableBeta { get; set; }
    public int MaxUsers { get; set; }
    
    public IEnumerable<ValidationError> CheckSettings()
    {
        if (EnableBeta && MaxUsers > 100)
            yield return "Beta features limited to 100 users";
    }
}
```

### Fixed Code

```csharp
using NexusLabs.Needlr.Generators;

// ✅ ValidateOnStart = true enables the validation method
[Options(ValidateMethod = "CheckSettings", ValidateOnStart = true)]
public class FeatureFlags
{
    public bool EnableBeta { get; set; }
    public int MaxUsers { get; set; }
    
    public IEnumerable<ValidationError> CheckSettings()
    {
        if (EnableBeta && MaxUsers > 100)
            yield return "Beta features limited to 100 users";
    }
}
```

## Using Convention-Based Naming

If you name your method `Validate` and set `ValidateOnStart = true`, you don't need to specify `ValidateMethod`:

```csharp
// Convention: "Validate" method is auto-discovered when ValidateOnStart = true
[Options(ValidateOnStart = true)]
public class CacheOptions
{
    public int TimeoutSeconds { get; set; }
    
    public IEnumerable<ValidationError> Validate()
    {
        if (TimeoutSeconds <= 0)
            yield return "TimeoutSeconds must be positive";
    }
}
```

## Suppressing the Warning

If you intentionally want to specify a validation method without running it at startup:

```csharp
#pragma warning disable NDLRGEN019
[Options(ValidateMethod = "CheckConfig")]
public class MyOptions { }
#pragma warning restore NDLRGEN019
```

## See Also

- [NDLRGEN018](NDLRGEN018.md) - Validator won't run
- [NDLRGEN016](NDLRGEN016.md) - Validation method not found
- [Options Documentation](../options.md)
