# NDLRMAF025: CreateGraphWorkflow is incompatible with GraphJoinMode.WaitAny

## Cause

Code calls `CreateGraphWorkflow("graphName")` on a graph that declares `[AgentGraphNode(JoinMode = GraphJoinMode.WaitAny)]` on one or more nodes.

## Rule Description

`CreateGraphWorkflow` returns a MAF `Workflow` object that uses Bulk Synchronous Parallel (BSP) execution. BSP uses mandatory synchronization barriers — every superstep waits for ALL active nodes to complete. There is no MAF primitive for "proceed when any upstream node completes."

Calling `CreateGraphWorkflow` on a graph with WaitAny nodes will throw `NotSupportedException` at runtime. This analyzer catches the problem at compile time.

Use `RunGraphAsync` instead — it auto-detects WaitAny nodes and uses Needlr's own graph executor with `Task.WhenAny` for fan-in points.

## How to Fix

Replace `CreateGraphWorkflow` with `RunGraphAsync`.

### Before

```csharp
// WRONG — will throw NotSupportedException at runtime
var workflow = factory.CreateGraphWorkflow("my-graph");
var results = await workflow.RunAsync(question);
```

### After

```csharp
// CORRECT — auto-selects MAF BSP for WaitAll, Needlr executor for WaitAny
var results = await factory.RunGraphAsync("my-graph", question);
```

## When to Suppress

Do not suppress this diagnostic. The code will throw `NotSupportedException` at runtime.

## See Also

- [AI Integrations — DAG Graph Workflows](../ai-integrations.md)
- [ADR-0001: DAG/Graph Workflow Support](../adr/adr-0001-dag-graph-workflow-support.md)
