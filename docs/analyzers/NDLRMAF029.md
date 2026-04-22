# NDLRMAF029: Reducer method not found or has wrong signature

## Cause

The `ReducerMethod` property on `[AgentGraphReducer]` references a method name that does not exist on the declaring class, is not `static`, does not return `string`, or does not accept `IReadOnlyList<string>`.

## Rule Description

A reducer aggregates the outputs from multiple parallel branches into a single input for the downstream node. At runtime, the method is invoked via reflection. If the method name is misspelled or has the wrong signature, the graph will fail at runtime.

This analyzer catches these mistakes at compile time.

## How to Fix

Ensure the reducer method exists on the class that declares the `[AgentGraphReducer]` attribute and has the correct signature.

```csharp
// WRONG — method name typo
[AgentGraphReducer("Pipeline", ReducerMethod = "Merg")]
public class MyReducer { }

// WRONG — not static
[AgentGraphReducer("Pipeline", ReducerMethod = "Merge")]
public class MyReducer
{
    public string Merge(IReadOnlyList<string> inputs) => string.Join(", ", inputs);
}

// CORRECT
[AgentGraphReducer("Pipeline", ReducerMethod = "Merge")]
public class MyReducer
{
    public static string Merge(IReadOnlyList<string> inputs) => string.Join(", ", inputs);
}
```

## See Also

- [AgentGraphReducerAttribute](../ai-integrations.md)
- [ADR-0001: DAG/Graph Workflow Support](../adr/adr-0001-dag-graph-workflow-support.md)
