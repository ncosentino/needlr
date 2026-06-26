# NDLRGEN035: [RegisterClosedOverImplementationsOf] source type must be an open generic interface

## Cause

The first type argument passed to `[RegisterClosedOverImplementationsOf]` is not an open generic interface (unbound generic type).

## Rule Description

The attribute discovers concrete closed implementations of an open generic interface and registers a composition closed over each implementation's type argument(s). The source type must be:

1. **An interface** (not a class or struct)
2. **An open/unbound generic** using the `typeof(IInterface<>)` syntax

Common mistakes include passing a closed generic like `typeof(IFooDefinition<string>)`, a non-generic interface like `typeof(IService)`, or a class instead of an interface.

## How to Fix

Use the open generic `typeof()` syntax with empty angle brackets:

```csharp
using NexusLabs.Needlr.Generators;

public interface IFooDefinition<T> { }
public interface IFoo { }

// ❌ WRONG - closed generic
[RegisterClosedOverImplementationsOf(typeof(IFooDefinition<string>), As = typeof(IFoo))]
public class FooCore<T> : IFoo { }

// ❌ WRONG - non-generic interface
[RegisterClosedOverImplementationsOf(typeof(IFoo), As = typeof(IFoo))]
public class OtherCore<T> : IFoo { }

// ✅ CORRECT - open generic interface
[RegisterClosedOverImplementationsOf(typeof(IFooDefinition<>), As = typeof(IFoo))]
public class GoodCore<T> : IFoo { }
```

## See Also

- [NDLRGEN036](NDLRGEN036.md) - Composition class must be an open generic with matching arity
- [NDLRGEN037](NDLRGEN037.md) - Composition class must implement the `As` service type
- [Compose and Expose Closed Generics](../composed-closed-generics.md)
