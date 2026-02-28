# NDLRMAF005: FunctionGroups references an unregistered group name

## Cause

An agent declares `FunctionGroups = new[] { "xyz" }` but no class in the compilation is decorated with `[AgentFunctionGroup("xyz")]`.

## Rule Description

Group names in `FunctionGroups` are matched at runtime against the registered `[AgentFunctionGroup]` classes. If the referenced name has no matching class, the agent will receive zero tools silently — there is no runtime error. This diagnostic catches that silent failure at compile time.

## How to Fix

Either register a class for the referenced group name, correct the spelling in `FunctionGroups`, or remove the group reference.

### Before

```csharp
[NeedlrAiAgent(FunctionGroups = new[] { "geography" })]
public class GeographyAgent { }

// No class has [AgentFunctionGroup("geography")]
```

### After

```csharp
[NeedlrAiAgent(FunctionGroups = new[] { "geography" })]
public class GeographyAgent { }

[AgentFunctionGroup("geography")]
public class GeographyFunctions
{
    [AgentFunction]
    public string GetCountriesLived() => "Canada, UK";
}
```

## When to Suppress

Only suppress if the function group class is defined in a separate assembly that Needlr's analyzer cannot see in this compilation unit.

```csharp
#pragma warning disable NDLRMAF005
[NeedlrAiAgent(FunctionGroups = new[] { "external-group" })]
public class MyAgent { }
#pragma warning restore NDLRMAF005
```

## See Also

- [AI Integrations — Multi-Agent Orchestration](../ai-integrations.md#multi-agent-orchestration)
- [NDLRMAF008](NDLRMAF008.md) — agent participates in no topology
