# NDLRGEN031: Provider class requires partial modifier

## Diagnostic Info

| Property | Value |
|----------|-------|
| ID | NDLRGEN031 |
| Category | NexusLabs.Needlr.Generators |
| Severity | Error |
| Enabled | Yes |

## Description

This error is raised when a class has the `[Provider]` attribute but is not declared with the `partial` modifier. The generator needs to add an interface implementation and constructor to the class, which requires it to be partial.

## Example

### Code that triggers the error

```csharp
using NexusLabs.Needlr.Generators;

// ❌ NDLRGEN031: Class needs partial modifier
[Provider(typeof(IOrderRepository))]
public class OrderProvider { }
```

### How to fix

Add the `partial` modifier to the class:

```csharp
using NexusLabs.Needlr.Generators;

// ✅ Correct: Class is partial
[Provider(typeof(IOrderRepository))]
public partial class OrderProvider { }
```

## Why This Matters

When using `[Provider]` on a class (shorthand mode), the source generator creates:

1. A generated interface (`IOrderProvider`)
2. A partial class implementation with constructor and properties

Without the `partial` modifier, the compiler cannot merge the generated code with your class definition.

## Alternative: Interface Mode

If you don't want a partial class, use interface mode instead:

```csharp
[Provider]
public interface IOrderProvider
{
    IOrderRepository Repository { get; }
}
```

The generator will create the implementation class for you in the `{AssemblyName}.Generated` namespace.

## See Also

- [Providers](../providers.md)
- [NDLRGEN032](NDLRGEN032.md) - Invalid interface member
