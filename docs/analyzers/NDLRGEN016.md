# NDLRGEN016: Validation method not found

## Cause

The `ValidateMethod` specified in `[Options]` does not exist on the target type.

## Rule Description

When you use `[Options(ValidateMethod = "MethodName")]`, the generator looks for a method with that name on:
1. The options class itself (if no `Validator` is specified)
2. The validator class (if `Validator` is specified)

If the method doesn't exist, this error is raised.

## How to Fix

Add the method with the correct name:

```csharp
// ❌ Wrong - method name doesn't match
[Options(ValidateMethod = "ValidateSettings", ValidateOnStart = true)]
public class AppSettings
{
    public string Name { get; set; } = "";
    
    // Method is named "Validate", not "ValidateSettings"
    public IEnumerable<ValidationError> Validate() { ... }
}

// ✅ Correct - method name matches
[Options(ValidateMethod = "ValidateSettings", ValidateOnStart = true)]
public class AppSettings
{
    public string Name { get; set; } = "";
    
    public IEnumerable<ValidationError> ValidateSettings()
    {
        if (string.IsNullOrEmpty(Name))
            yield return "Name is required";
    }
}
```

## Example

### Code with Error

```csharp
using NexusLabs.Needlr.Generators;

// NDLRGEN016: Method 'CheckValidity' not found on type 'DatabaseOptions'
[Options(ValidateMethod = "CheckValidity", ValidateOnStart = true)]
public class DatabaseOptions
{
    public string ConnectionString { get; set; } = "";
    
    // No method named "CheckValidity" exists!
}
```

### Fixed Code

```csharp
using NexusLabs.Needlr.Generators;

[Options(ValidateMethod = "CheckValidity", ValidateOnStart = true)]
public class DatabaseOptions
{
    public string ConnectionString { get; set; } = "";
    
    // ✅ Added the method with matching name
    public IEnumerable<ValidationError> CheckValidity()
    {
        if (string.IsNullOrEmpty(ConnectionString))
            yield return "ConnectionString is required";
    }
}
```

## Using Convention-Based Naming

If you name your method `Validate`, you don't need to specify `ValidateMethod`:

```csharp
// No need for ValidateMethod - "Validate" is discovered by convention
[Options(ValidateOnStart = true)]
public class CacheOptions
{
    public int TimeoutSeconds { get; set; }
    
    // Convention: method named "Validate" is auto-discovered
    public IEnumerable<ValidationError> Validate()
    {
        if (TimeoutSeconds <= 0)
            yield return "TimeoutSeconds must be positive";
    }
}
```

## See Also

- [NDLRGEN014](NDLRGEN014.md) - Validator type has no validation method
- [NDLRGEN017](NDLRGEN017.md) - Validation method has wrong signature
- [Options Documentation](../options.md)
