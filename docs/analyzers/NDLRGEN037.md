# NDLRGEN037: [RegisterClosedOverImplementationsOf] composition must implement the As service type

## Cause

The class annotated with `[RegisterClosedOverImplementationsOf]` either does not specify an `As` service type, or specifies one it does not implement.

## Rule Description

Each closed composition is registered as the service type named by `As`, so the composition class must:

1. **Specify** an `As` type (e.g. `As = typeof(IFoo)`), and
2. **Implement** it, directly or transitively.

Without a valid `As` the generated registration would not compile (or would do nothing), so the attribute is meaningless.

## How to Fix

Set `As` to a service type the composition implements:

```csharp
using NexusLabs.Needlr.Generators;

public interface IFooDefinition<T> { }
public interface IFoo { }
public interface IOther { }

// ❌ WRONG - As is not specified
[RegisterClosedOverImplementationsOf(typeof(IFooDefinition<>))]
public class FooCore<T> : IFoo { }

// ❌ WRONG - composition does not implement the As type
[RegisterClosedOverImplementationsOf(typeof(IFooDefinition<>), As = typeof(IFoo))]
public class OtherCore<T> : IOther { }

// ✅ CORRECT - As is specified and implemented
[RegisterClosedOverImplementationsOf(typeof(IFooDefinition<>), As = typeof(IFoo))]
public class FooCore<T> : IFoo { }
```

## See Also

- [NDLRGEN035](NDLRGEN035.md) - Source type must be an open generic interface
- [NDLRGEN036](NDLRGEN036.md) - Composition class must be an open generic with matching arity
- [Compose and Expose Closed Generics](../composed-closed-generics.md)
