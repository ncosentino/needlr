# NDLRGEN036: [RegisterClosedOverImplementationsOf] composition must be an open generic class

## Cause

The class annotated with `[RegisterClosedOverImplementationsOf]` is not an open generic class, or its type-parameter count does not match the source open generic interface's arity.

## Rule Description

The generator closes the composition class over the type argument(s) of each discovered implementation of the source interface. For that to be possible, the composition class must be an open generic with the **same number of type parameters** as the source interface.

## How to Fix

Make the composition class generic with arity matching the source interface:

```csharp
using NexusLabs.Needlr.Generators;

public interface IFooDefinition<T> { }
public interface IFoo { }

// ❌ WRONG - composition is not generic
[RegisterClosedOverImplementationsOf(typeof(IFooDefinition<>), As = typeof(IFoo))]
public class FooCore : IFoo { }

// ✅ CORRECT - one type parameter to match IFooDefinition<>
[RegisterClosedOverImplementationsOf(typeof(IFooDefinition<>), As = typeof(IFoo))]
public class FooCore<T> : IFoo { }
```

For multi-parameter source interfaces, match the arity exactly:

```csharp
public interface IPairDefinition<TKey, TValue> { }

// ✅ CORRECT - two type parameters to match IPairDefinition<,>
[RegisterClosedOverImplementationsOf(typeof(IPairDefinition<,>), As = typeof(IPair))]
public class PairCore<TKey, TValue> : IPair { }
```

## See Also

- [NDLRGEN035](NDLRGEN035.md) - Source type must be an open generic interface
- [NDLRGEN037](NDLRGEN037.md) - Composition class must implement the `As` service type
- [Compose and Expose Closed Generics](../composed-closed-generics.md)
