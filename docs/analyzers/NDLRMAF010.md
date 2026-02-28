# NDLRMAF010: Termination condition type does not implement IWorkflowTerminationCondition

## Cause

The type referenced in `[AgentTerminationCondition(typeof(X))]` or `[WorkflowRunTerminationCondition(typeof(X))]` does not implement `IWorkflowTerminationCondition`.

## Rule Description

Termination condition types are instantiated at runtime via `Activator.CreateInstance` and cast to `IWorkflowTerminationCondition`. If the type does not implement this interface, the cast will fail with an `InvalidCastException` at the point the workflow is first executed. This diagnostic catches the problem at compile time.

## How to Fix

Implement `IWorkflowTerminationCondition` on the referenced type, or replace the reference with a built-in condition type such as `KeywordTerminationCondition` or `RegexTerminationCondition`.

### Before

```csharp
public class MyStopCondition   // ← does not implement IWorkflowTerminationCondition
{
    public bool Check(string text) => text.Contains("STOP");
}

[NeedlrAiAgent]
[AgentTerminationCondition(typeof(MyStopCondition))]
public class ReviewerAgent { }
```

### After

```csharp
public class MyStopCondition : IWorkflowTerminationCondition
{
    public bool ShouldTerminate(TerminationContext context)
        => context.ResponseText.Contains("STOP");
}

[NeedlrAiAgent]
[AgentTerminationCondition(typeof(MyStopCondition))]
public class ReviewerAgent { }
```

## When to Suppress

This should not be suppressed. A condition type that does not implement the interface will always fail at runtime.

## See Also

- [AI Integrations — Multi-Agent Orchestration](../ai-integrations.md#multi-agent-orchestration)
- [NDLRMAF009](NDLRMAF009.md) — condition declared on a non-agent class
