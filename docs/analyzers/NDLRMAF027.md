# NDLRMAF027: Terminal node has outgoing edges

## Cause

A node marked with `[AgentGraphNode(IsTerminal = true)]` also has outgoing `[AgentGraphEdge]` declarations.

## Rule Description

A node marked with `IsTerminal = true` on `[AgentGraphNode]` is expected to be a leaf node with no outgoing edges. Having both `IsTerminal = true` and `[AgentGraphEdge]` declarations is contradictory — the node cannot simultaneously be a terminal endpoint and route to other agents. Either remove `IsTerminal = true` or remove the outgoing edges.

## How to Fix

Remove `IsTerminal = true` if the node should route to other agents, or remove the `[AgentGraphEdge]` declarations if it should be a terminal node.

### Before

```csharp
[NeedlrAiAgent]
[AgentGraphEntry("pipeline")]
[AgentGraphEdge("pipeline", typeof(ReviewerAgent))]
public class PlannerAgent { }

[NeedlrAiAgent]
[AgentGraphNode("pipeline", IsTerminal = true)] // marked terminal
[AgentGraphEdge("pipeline", typeof(CoderAgent))] // but has outgoing edge
public class ReviewerAgent { }

[NeedlrAiAgent]
[AgentGraphNode("pipeline", IsTerminal = true)]
public class CoderAgent { }
```

### After (option A — remove IsTerminal)

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

### After (option B — remove the outgoing edge)

```csharp
[NeedlrAiAgent]
[AgentGraphEntry("pipeline")]
[AgentGraphEdge("pipeline", typeof(ReviewerAgent))]
public class PlannerAgent { }

[NeedlrAiAgent]
[AgentGraphNode("pipeline", IsTerminal = true)]
public class ReviewerAgent { }
```

## When to Suppress

Do not suppress this diagnostic. The combination of terminal flag and outgoing edges is always contradictory.

```csharp
#pragma warning disable NDLRMAF027
// Not recommended — contradictory node configuration
#pragma warning restore NDLRMAF027
```

## See Also

- [AI Integrations — Multi-Agent Orchestration](../ai-integrations.md#multi-agent-orchestration)
- [NDLRMAF016](NDLRMAF016.md) — cycle detected in agent graph
- [NDLRMAF024](NDLRMAF024.md) — all edges from fan-out node are optional
