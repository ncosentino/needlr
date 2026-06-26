# NDLRGEN038: [RegisterClosedOverImplementationsOf] discovered type argument violates composition constraints

## Cause

An implementation of the designated open generic interface was discovered whose type argument(s) do not satisfy the composition type's generic constraints. The corresponding registration is **skipped** and this warning is reported.

## Rule Description

Unlike [NDLRGEN035](NDLRGEN035.md)–[NDLRGEN037](NDLRGEN037.md), this diagnostic is emitted by the **generator**, not by a per-attribute analyzer, because only the generator knows the full set of discovered implementations. When a discovered type argument cannot legally close the composition, emitting `new Composition<TArg>(...)` would fail to compile; the generator skips that registration and reports this warning instead, turning a would-be runtime absence into a build-time signal.

This diagnostic is only **reachable** when the composition's constraints are *stricter* than the source interface's. If both carry the same constraint (e.g. `where TData : class`), every discovered implementation can always close the composition and this never fires.

## How to Fix

Align the constraints, or prevent the incompatible implementation from being discovered:

```csharp
using NexusLabs.Needlr.Generators;

// The source interface is unconstrained, so a struct argument is allowed here...
public interface IFooDefinition<TData> { }
public struct StructData { }
public sealed class StructFoo : IFooDefinition<StructData> { }

public interface IFoo { }

// ❌ WRONG - composition requires a reference type, but StructFoo closes over a struct.
//    NDLRGEN038 is reported and the StructData registration is skipped.
[RegisterClosedOverImplementationsOf(typeof(IFooDefinition<>), As = typeof(IFoo))]
public sealed class FooCore<TData> : IFoo where TData : class { }
```

Fixes include:

- **Constrain the source interface** so incompatible implementations cannot exist (`public interface IFooDefinition<TData> where TData : class`).
- **Relax the composition's constraint** if the composition genuinely supports the wider set.
- **Exclude the implementation** if it should not participate.

## See Also

- [NDLRGEN035](NDLRGEN035.md) - Source type must be an open generic interface
- [NDLRGEN036](NDLRGEN036.md) - Composition class must be an open generic with matching arity
- [NDLRGEN037](NDLRGEN037.md) - Composition class must implement the `As` service type
- [Compose and Expose Closed Generics](../composed-closed-generics.md)
