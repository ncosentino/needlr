# NDLRMAF024: All edges from fan-out node are optional

## Cause

A fan-out node (a node with multiple outgoing edges) has all edges marked as `IsRequired = false`.

## Rule Description

When all outgoing edges from a fan-out node have `IsRequired = false`, every downstream branch is optional. If all optional branches fail at runtime, the downstream nodes receive no input and the graph may produce empty or unexpected results. Consider making at least one edge required to guarantee that the graph produces meaningful output.

## How to Fix

Set `IsRequired = true` on at least one outgoing edge to ensure the graph always has a guaranteed execution path.

### Before

```csharp
[NeedlrAiAgent]
[AgentGraphEntry("pipeline")]
[AgentGraphEdge("pipeline", typeof(ReviewerAgent), IsRequired = false)]
[AgentGraphEdge("pipeline", typeof(QaAgent), IsRequired = false)]
public class PlannerAgent { } // all edges optional

[NeedlrAiAgent]
[AgentGraphNode("pipeline", IsTerminal = true)]
public class ReviewerAgent { }

[NeedlrAiAgent]
[AgentGraphNode("pipeline", IsTerminal = true)]
public class QaAgent { }
```

### After

```csharp
[NeedlrAiAgent]
[AgentGraphEntry("pipeline")]
[AgentGraphEdge("pipeline", typeof(ReviewerAgent), IsRequired = true)]
[AgentGraphEdge("pipeline", typeof(QaAgent), IsRequired = false)]
public class PlannerAgent { } // at least one required edge

[NeedlrAiAgent]
[AgentGraphNode("pipeline", IsTerminal = true)]
public class ReviewerAgent { }

[NeedlrAiAgent]
[AgentGraphNode("pipeline", IsTerminal = true)]
public class QaAgent { }
```

## When to Suppress

Suppress if empty results from the fan-out are acceptable for your workflow and you handle the case downstream (e.g. via a fallback or default response).

```csharp
#pragma warning disable NDLRMAF024
[AgentGraphEdge("pipeline", typeof(ReviewerAgent), IsRequired = false)]
[AgentGraphEdge("pipeline", typeof(QaAgent), IsRequired = false)]
public class PlannerAgent { }
#pragma warning restore NDLRMAF024
```

## See Also

- [AI Integrations — Multi-Agent Orchestration](../ai-integrations.md#multi-agent-orchestration)
- [NDLRMAF016](NDLRMAF016.md) — cycle detected in agent graph
- [NDLRMAF027](NDLRMAF027.md) — terminal node has outgoing edges
