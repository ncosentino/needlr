# NDLRMAF028: Condition method not found or has wrong signature

## Cause

The `Condition` property on `[AgentGraphEdge]` references a method name that does not exist on the declaring class, is not `static`, does not return `bool`, or does not accept a single `object?` parameter.

## Rule Description

When an `[AgentGraphEdge]` specifies a `Condition`, the runtime invokes that method at execution time to decide whether to follow the edge. If the method name is misspelled, has the wrong signature, or is an instance method, the edge will fail at runtime with an `InvalidOperationException`.

This analyzer catches these mistakes at compile time.

## How to Fix

Ensure the condition method exists on the class that declares the `[AgentGraphEdge]` attribute and has the correct signature.

```csharp
// WRONG — method name typo
[AgentGraphEdge("Pipeline", typeof(WorkerAgent), Condition = "ShouldRout")]
public class EntryAgent { }

// WRONG — not static
[AgentGraphEdge("Pipeline", typeof(WorkerAgent), Condition = "ShouldRoute")]
public class EntryAgent
{
    public bool ShouldRoute(object? input) => true;
}

// CORRECT
[AgentGraphEdge("Pipeline", typeof(WorkerAgent), Condition = "ShouldRoute")]
public class EntryAgent
{
    public static bool ShouldRoute(object? input) => true;
}
```

## See Also

- [AgentGraphEdgeAttribute](../ai-integrations.md)
- [ADR-0001: DAG/Graph Workflow Support](../adr/adr-0001-dag-graph-workflow-support.md)
