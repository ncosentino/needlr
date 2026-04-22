# NDLRMAF016: Cycle detected in agent graph

## Cause

A named agent graph declared via `[AgentGraphEdge]` attributes contains a cycle.

## Rule Description

Agent graph workflows use superstep-based BSP (Bulk Synchronous Parallel) execution, which requires a directed acyclic graph (DAG). A cycle in the graph — where an agent can eventually reach itself through a chain of edges — will cause infinite execution at runtime.

The analyzer performs a topological sort across all `[AgentGraphEdge]` declarations within each named graph and reports an error when a back-edge is detected.

## How to Fix

Break the cycle by removing or redirecting one of the edges that forms the loop.

### Before

```csharp
[NeedlrAiAgent]
[AgentGraphEntry("pipeline")]
[AgentGraphEdge("pipeline", typeof(ReviewerAgent))]
public class PlannerAgent { }

[NeedlrAiAgent]
[AgentGraphEdge("pipeline", typeof(CoderAgent))]
public class ReviewerAgent { }

[NeedlrAiAgent]
[AgentGraphEdge("pipeline", typeof(PlannerAgent))] // cycle: Planner → Reviewer → Coder → Planner
public class CoderAgent { }
```

### After

```csharp
[NeedlrAiAgent]
[AgentGraphEntry("pipeline")]
[AgentGraphEdge("pipeline", typeof(ReviewerAgent))]
public class PlannerAgent { }

[NeedlrAiAgent]
[AgentGraphEdge("pipeline", typeof(CoderAgent))]
public class ReviewerAgent { }

[NeedlrAiAgent]
[AgentGraphNode("pipeline", IsTerminal = true)]
public class CoderAgent { }
```

## When to Suppress

Do not suppress this diagnostic. Cycles in agent graphs always cause infinite execution at runtime. If you need iterative behaviour, consider using a handoff topology or a group chat with termination conditions instead.

```csharp
#pragma warning disable NDLRMAF016
// Not recommended — cycles cause infinite execution
#pragma warning restore NDLRMAF016
```

## See Also

- [AI Integrations — Multi-Agent Orchestration](../ai-integrations.md#multi-agent-orchestration)
- [NDLRMAF017](NDLRMAF017.md) — graph has no entry point
- [NDLRMAF022](NDLRMAF022.md) — graph contains unreachable agents
