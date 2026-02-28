# NDLRMAF007: Gap in sequential pipeline Order values

## Cause

The `Order` values declared for a sequential pipeline are non-consecutive (e.g. 1, 2, 4 — missing 3).

## Rule Description

Gaps in order values suggest a step was removed without updating the remaining values, or an agent was accidentally skipped. While the pipeline will still execute in the declared order at runtime, the gap is a likely authoring error. This diagnostic is informational — the pipeline will function correctly with gaps.

## How to Fix

Renumber the agents in the pipeline to fill the gap, or confirm the gap is intentional.

### Before

```csharp
[NeedlrAiAgent]
[AgentSequenceMember("content-pipeline", Order = 1)]
public class ExtractorAgent { }

[NeedlrAiAgent]
[AgentSequenceMember("content-pipeline", Order = 2)]
public class EnricherAgent { }

[NeedlrAiAgent]
[AgentSequenceMember("content-pipeline", Order = 4)]  // ← gap: 3 is missing
public class PublisherAgent { }
```

### After

```csharp
[NeedlrAiAgent]
[AgentSequenceMember("content-pipeline", Order = 1)]
public class ExtractorAgent { }

[NeedlrAiAgent]
[AgentSequenceMember("content-pipeline", Order = 2)]
public class EnricherAgent { }

[NeedlrAiAgent]
[AgentSequenceMember("content-pipeline", Order = 3)]  // ← consecutive
public class PublisherAgent { }
```

## When to Suppress

Suppress if you are deliberately reserving order values for future agents and want to avoid renumbering.

```csharp
#pragma warning disable NDLRMAF007
[AgentSequenceMember("content-pipeline", Order = 10)]
public class PublisherAgent { }
#pragma warning restore NDLRMAF007
```

## See Also

- [AI Integrations — Multi-Agent Orchestration](../ai-integrations.md#multi-agent-orchestration)
- [NDLRMAF006](NDLRMAF006.md) — duplicate order value
