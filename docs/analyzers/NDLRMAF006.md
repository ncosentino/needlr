# NDLRMAF006: Duplicate Order value in sequential pipeline

## Cause

Two or more agents in the same sequential pipeline share the same `Order` value in their `[AgentSequenceMember]` attribute.

## Rule Description

The `Order` value determines execution sequence within the pipeline. Duplicate order values create an ambiguous sequence — the generator cannot determine which agent runs first, and the resulting behavior is undefined. This diagnostic fires at compilation end.

> **Note:** This is a compilation-end diagnostic. It appears after the build completes, not as you type.

## How to Fix

Assign a unique `Order` value to each agent in the pipeline. Convention is to use consecutive integers starting from 1.

### Before

```csharp
[NeedlrAiAgent]
[AgentSequenceMember("content-pipeline", Order = 1)]
public class ExtractorAgent { }

[NeedlrAiAgent]
[AgentSequenceMember("content-pipeline", Order = 1)]  // ← duplicate
public class EnricherAgent { }
```

### After

```csharp
[NeedlrAiAgent]
[AgentSequenceMember("content-pipeline", Order = 1)]
public class ExtractorAgent { }

[NeedlrAiAgent]
[AgentSequenceMember("content-pipeline", Order = 2)]  // ← unique
public class EnricherAgent { }
```

## When to Suppress

This should not be suppressed. Duplicate order values produce unpredictable pipeline execution.

## See Also

- [AI Integrations — Multi-Agent Orchestration](../ai-integrations.md#multi-agent-orchestration)
- [NDLRMAF007](NDLRMAF007.md) — gap in sequential pipeline order values
