# NDLRGEN017: Validation method has wrong signature

## Cause

A validation method exists but has an incorrect signature.

## Rule Description

Validation methods must follow specific signatures to be recognized by the generator:

**For methods on the options class itself:**
```csharp
IEnumerable<ValidationError> Validate()
IEnumerable<string> Validate()
```

**For methods on an external validator:**
```csharp
IEnumerable<ValidationError> Validate(TOptions options)
IEnumerable<string> Validate(TOptions options)
```

If your method doesn't match one of these patterns, this error is raised.

## How to Fix

Correct the method signature:

```csharp
// ❌ Wrong - returns void instead of IEnumerable
[Options(ValidateOnStart = true)]
public class ApiOptions
{
    public string Key { get; set; } = "";
    
    public void Validate()  // Wrong return type!
    {
        if (string.IsNullOrEmpty(Key))
            throw new Exception("Key required");
    }
}

// ✅ Correct - returns IEnumerable<ValidationError>
[Options(ValidateOnStart = true)]
public class ApiOptions
{
    public string Key { get; set; } = "";
    
    public IEnumerable<ValidationError> Validate()
    {
        if (string.IsNullOrEmpty(Key))
            yield return "Key is required";
    }
}
```

## Example

### Code with Error

```csharp
using NexusLabs.Needlr.Generators;

// NDLRGEN017: Method 'Validate' on type 'EmailOptions' has wrong signature
[Options(ValidateOnStart = true)]
public class EmailOptions
{
    public string SmtpHost { get; set; } = "";
    
    // Wrong: takes a parameter (should be parameterless for instance methods)
    public IEnumerable<string> Validate(bool strict)
    {
        if (string.IsNullOrEmpty(SmtpHost))
            yield return "SmtpHost required";
    }
}
```

### Fixed Code

```csharp
using NexusLabs.Needlr.Generators;

[Options(ValidateOnStart = true)]
public class EmailOptions
{
    public string SmtpHost { get; set; } = "";
    
    // ✅ Correct: no parameters for instance method on options class
    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrEmpty(SmtpHost))
            yield return "SmtpHost required";
    }
}
```

## Valid Signatures

### On Options Class (instance method, no parameters)

```csharp
public IEnumerable<ValidationError> Validate()
public IEnumerable<string> Validate()
```

### On External Validator (takes options as parameter)

```csharp
// Instance method
public IEnumerable<ValidationError> Validate(TOptions options)
public IEnumerable<string> Validate(TOptions options)

// Static method
public static IEnumerable<ValidationError> Validate(TOptions options)
public static IEnumerable<string> Validate(TOptions options)
```

## Common Mistakes

| Wrong Signature | Problem | Correct Signature |
|-----------------|---------|-------------------|
| `void Validate()` | Wrong return type | `IEnumerable<ValidationError> Validate()` |
| `bool Validate()` | Wrong return type | `IEnumerable<string> Validate()` |
| `List<string> Validate()` | Must be IEnumerable | `IEnumerable<string> Validate()` |
| `IEnumerable<ValidationError> Validate(bool flag)` | Extra parameter | `IEnumerable<ValidationError> Validate()` |

## See Also

- [NDLRGEN014](NDLRGEN014.md) - Validator type has no validation method
- [NDLRGEN016](NDLRGEN016.md) - Validation method not found
- [Options Documentation](../options.md)
