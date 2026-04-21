---
title: "ADR-0001: DAG/Graph Workflow Support"
status: "Proposed"
date: "2025-07-18"
authors: "Nick Cosentino"
tags: ["architecture", "decision", "agent-framework", "workflows", "dag", "maf"]
supersedes: ""
superseded_by: ""
---

## Status

Proposed

## Context

Needlr's agent framework wraps [Microsoft Agent Framework (MAF)](https://github.com/microsoft/agent-framework) with an attribute-driven, source-generated workflow model. Three workflow types exist today:

| Workflow Type | Attribute | Factory Method Pattern |
|---|---|---|
| Handoff | `[AgentHandoffsTo]` | `Create{Agent}HandoffWorkflow()` |
| Group Chat | `[AgentGroupChatMember]` | `Create{GroupName}GroupChatWorkflow()` |
| Sequential | `[AgentSequenceMember]` | `Create{PipelineName}SequentialWorkflow()` |

Each follows a consistent pattern: attributes on agent classes declare topology, a source generator (`AgentFrameworkFunctionRegistryGenerator`) emits `IWorkflowFactory` extension methods, and Roslyn analyzers (`NDLRMAF001`–`NDLRMAF015`) provide compile-time validation.

MAF 1.1.0 introduced a graph-based execution model (`WorkflowBuilder`, `Edge`, `Executor`, `FunctionExecutor<T>`, `DirectEdgeData`, `FanOutEdgeData`, `FanInEdgeData`, `SwitchBuilder`) using superstep-based Bulk Synchronous Parallel (BSP) execution. This model enables directed acyclic graph (DAG) orchestration: conditional routing, fan-out/fan-in parallelism, and non-linear agent coordination. Needlr does not yet wrap this API.

Without DAG support, users who need branching, parallel, or conditional workflows must drop down to raw MAF APIs, losing Needlr's compile-time validation, diagnostics integration, and attribute-driven developer experience.

Existing infrastructure that this decision builds on:

- **Progress reporting**: `IProgressReporter` with events for workflow lifecycle, agent invocation, LLM calls, tool calls, budget tracking, and superstep progression (`SuperStepStartedProgressEvent`, `SuperStepCompletedProgressEvent`).
- **Diagnostics**: `IPipelineRunResult` with per-stage `IAgentStageResult` entries, token usage tracking via `IAgentMetrics`, and hierarchical scoping via `BeginChildScope`.
- **Source generator**: Incremental generator with `Models/` (metadata types like `HandoffEntry`, `GroupChatEntry`, `SequenceEntry`) and `CodeGen/` (emitters like `RegistryCodeGenerator`, `ExtensionsCodeGenerator`, `BootstrapCodeGenerator`, `TopologyGraphCodeGenerator`).
- **Analyzer infrastructure**: `MafDiagnosticIds` and `MafDiagnosticDescriptors` centralize diagnostic metadata. Existing analyzers cover cyclic handoffs, orphan detection, group chat validation, sequence ordering, and topology correctness.

### Forces

- **Consistency**: A 4th workflow type should follow the established attribute → generator → analyzer → factory pattern.
- **Determinism**: Orchestration edges encode control flow. Nondeterministic routing by default undermines testability, replayability, and debuggability.
- **Expressiveness**: DAGs introduce topological ambiguity that linear and group-chat workflows do not have. Three outgoing conditional edges could mean fan-out, switch-case, or priority routing — the topology alone does not disambiguate.
- **Progressive disclosure**: Simple DAGs (linear chains, basic fan-out) should be simple to declare. Complex DAGs (conditional routing, LLM-driven choice, mixed fan-out/fan-in) should be possible without escaping to raw MAF.
- **MAF stability**: MAF's graph API is relatively new (v1.1.0). The attribute model should insulate users from breaking changes in the underlying API.

## Decision

Add a 4th workflow type — DAG/graph workflows — via `[AgentGraphEdge]` and `[AgentGraphEntry]` attributes, a `[AgentGraphReducer]` attribute for fan-in aggregation, source-generated `Create{Name}GraphWorkflow()` factory methods, and 8 new Roslyn analyzers (`NDLRMAF016`–`NDLRMAF023`).

### Core Attribute Design

Edges are declared on the source node (edge-on-source), consistent with `[AgentHandoffsTo]`:

```csharp
[AgentGraphEntry("ResearchPipeline", MaxSupersteps = 15)]
[AgentGraphEdge("ResearchPipeline", typeof(WebResearchAgent), Condition = "needs-web")]
[AgentGraphEdge("ResearchPipeline", typeof(SummarizerAgent))]
public class AnalyzerAgent { }
```

- `[AgentGraphEntry]` marks an agent as the entry point for a named graph. It is required and must be explicit — entry points are not inferred from topology.
- `[AgentGraphEdge]` declares a directed edge from the decorated class to the specified target agent type, within a named graph.
- The source generator emits `Create{Name}GraphWorkflow()` as an extension method on `IWorkflowFactory`.

### Routing: Deterministic Default, LLM Opt-In

Deterministic routing is the default. Condition strings on `[AgentGraphEdge]` reference a named predicate method on the agent class by convention. The source generator validates the method exists and emits the `Func<object?, bool>` binding for MAF's `DirectEdgeData.Condition`.

LLM-driven routing is an explicit opt-in via `RoutingMode = GraphRoutingMode.LlmChoice` on the entry point attribute. When active, condition strings become handoff-style tool descriptions — the agent's LLM selects which edge to follow. The routing decision is recorded in diagnostics for auditability.

Routing mode enum (`GraphRoutingMode`):

| Mode | Behavior |
|---|---|
| `Deterministic` (default) | Condition methods evaluated as boolean predicates |
| `LlmChoice` | Condition strings as tool descriptions; model selects |
| `AllMatching` | All edges whose condition is true are followed (parallel fan-out) |
| `FirstMatching` | First edge whose condition is true is followed (priority order) |

**Rationale**: DAG edges encode orchestration rules. Making control flow nondeterministic by default undermines testability, replayability, and determinism. LLM routing is powerful but should be a conscious architectural choice.

### Fan-Out/Fan-In: Inferred Shape, Explicit Semantics

Fan-out and fan-in **shapes** are inferred from topology (multiple outgoing edges = fan-out, multiple incoming edges = fan-in). However, the **semantics** are explicitly declared because topology alone is ambiguous.

Source-side routing (on `[AgentGraphEntry]` or per-node):

- `AllMatching` — run all edges whose condition passes
- `FirstMatching` — run the first edge whose condition passes
- `ExclusiveChoice` — exactly one edge must match (analyzer error if ambiguous at compile time)
- `LlmChoice` — LLM selects

Target-side join (on `[AgentGraphEdge]` or per-node):

- `WaitAll` (default) — barrier; wait for all incoming edges before proceeding
- `WaitAny` — proceed when any incoming edge completes

**Rationale**: Three conditional outgoing edges could mean fan-out, switch-case, or priority routing. The topology says "3 edges" but not what they mean. Explicit semantics prevent runtime ambiguity without requiring users to understand MAF's `FanOutEdgeData` vs `DirectEdgeData` distinction.

### Non-Agent Nodes: Minimal Phase-1 Reducer

Full `FunctionExecutor<T>` support is deferred to phase 2. However, phase 1 includes a minimal deterministic reducer node for fan-in convergence, because fan-in without a reducer forces users to wrap pure aggregation logic in agent classes (paying LLM cost and latency for what should be a deterministic function).

```csharp
[AgentGraphReducer("ResearchPipeline", ReducerMethod = nameof(MergeResults))]
public static class ResearchReducer
{
    public static string MergeResults(IReadOnlyList<string> branchOutputs)
        => string.Join("\n---\n", branchOutputs);
}
```

The reducer covers the most common fan-in pattern (aggregation of branch outputs) without requiring a full function-executor attribute model.

### Streaming: Terminal Content, Full Progress

Content streaming is terminal-node only by default. Intermediate node output is internal processing — not user-facing.

Progress and observability use the existing `IProgressReporter` infrastructure, which already supports `AgentInvokedEvent`, `LlmCallStartedEvent`, `ToolCallCompletedEvent`, and `SuperStepStartedProgressEvent`. DAG-specific metadata (branch ID, node ID, edge traversal) is added to existing progress events rather than introducing a parallel observation API.

**Rationale**: Users need progress visibility for long-running DAGs, but that is a progress concern, not a streaming concern. The existing `IProgressReporter` and `IProgressSink` pipeline is extensible and already consumed by downstream tooling.

### Failure Propagation: Per-Edge Required/Optional

Graph-wide failure modes (fail-fast vs continue-parallel) are too coarse for DAGs with heterogeneous branches. Instead, failure semantics are declared per-edge:

- `IsRequired = true` (default): if this edge's target node fails, the entire graph fails.
- `IsRequired = false`: if this edge's target node fails, the branch is marked degraded but parallel branches continue.

Completed branch outputs are always preserved in `IDagRunResult.NodeResults` — even when the graph fails, work already done is accessible. This avoids discarding expensive computation (e.g., a completed research branch) when an optional enrichment branch fails.

### Diagnostics: `IDagRunResult`

`IDagRunResult` extends `IPipelineRunResult` with:

- `NodeResults` — per-node diagnostics with edge metadata and timing offsets
- `BranchResults` — parallel branch grouping with degraded/failed status
- Flat `Stages` preserved for backward compatibility with existing `IPipelineRunResult` consumers
- Token budget tracking via existing hierarchical scoping (`BeginChildScope`)

### Analyzers

Eight new analyzers extending the existing `NDLRMAF` series:

| ID | Title | Severity |
|---|---|---|
| NDLRMAF016 | Cycle detected in agent graph | Error |
| NDLRMAF017 | Graph has no entry point | Error |
| NDLRMAF018 | Graph has multiple entry points | Error |
| NDLRMAF019 | Graph edge target is not a declared agent | Error |
| NDLRMAF020 | Graph edge source is not a declared agent | Warning |
| NDLRMAF021 | Graph entry point is not a declared agent | Warning |
| NDLRMAF022 | Graph contains unreachable agents | Warning |
| NDLRMAF023 | MaxSupersteps value is invalid (≤ 0) | Error |

These follow the existing pattern in `MafDiagnosticIds` and `MafDiagnosticDescriptors`, extending the ID range from the current ceiling of `NDLRMAF015`.

### Implementation Phases

| Phase | Scope | Deliverables |
|---|---|---|
| 1 | Attributes + Runtime Factory + Minimal Reducer | `AgentGraphEdgeAttribute`, `AgentGraphEntryAttribute`, `AgentGraphReducerAttribute`, `GraphRoutingMode` enum, `WorkflowFactory` graph support |
| 2 | Source Generator + Mermaid Diagrams | `GraphEntry` model, `GraphCodeGenerator`, `TopologyGraphCodeGenerator` Mermaid output, `BootstrapCodeGenerator` graph registration |
| 3 | Analyzers + Release Tracking + Docs | `NDLRMAF016`–`NDLRMAF023`, analyzer tests, XML doc comments, README updates |
| 4 | Diagnostics + Progress Events + Example App | `IDagRunResult`, DAG-specific progress event metadata, `Examples/` project demonstrating a research pipeline |

## Consequences

### Positive

- **POS-001**: Needlr covers all four MAF workflow patterns (Handoff, Group Chat, Sequential, DAG), eliminating the need for users to drop to raw MAF APIs for non-linear orchestration.
- **POS-002**: Deterministic-first routing gives users testable, replayable, and debuggable orchestration by default. LLM-driven routing is available when intelligence-based decisions are genuinely needed.
- **POS-003**: Compile-time validation via 8 new analyzers catches topology errors (cycles, missing entry points, unreachable nodes) before runtime, consistent with the existing analyzer experience for other workflow types.
- **POS-004**: Per-edge failure semantics (`IsRequired`) preserve expensive parallel work when optional branches fail, avoiding the all-or-nothing tradeoff of graph-wide failure modes.
- **POS-005**: The attribute model insulates users from MAF's graph API surface (`WorkflowBuilder`, `Edge`, `DirectEdgeData`, `FanOutEdgeData`, etc.), providing a stable abstraction layer if MAF's API evolves.

### Negative

- **NEG-001**: Significant implementation surface across 4 phases: 3 new attributes, 1 enum, factory method updates, 8 analyzers, generator updates, a new diagnostics interface, and progress event extensions.
- **NEG-002**: Routing mode (`Deterministic`, `LlmChoice`, `AllMatching`, `FirstMatching`) and join mode (`WaitAll`, `WaitAny`) add cognitive overhead compared to the simpler "just declare edges" model. Users must understand when each mode applies.
- **NEG-003**: The phase-1 reducer (`[AgentGraphReducer]`) is a narrow solution covering only the aggregation pattern. Full `FunctionExecutor<T>` support is still needed for arbitrary non-agent computation nodes.
- **NEG-004**: MAF's graph execution model uses superstep-based BSP, which may surprise users expecting event-driven or streaming DAG execution. Documentation must set clear expectations.

## Alternatives Considered

### Infer Entry Points from Topology

- **Description**: Automatically identify the entry point as the node with zero incoming edges, rather than requiring an explicit `[AgentGraphEntry]` attribute.
- **Rejection Reason**: Topological inference is fragile — a graph with multiple roots (e.g., parallel starting branches) would require disambiguation logic in the generator. Explicit declaration is consistent with the existing pattern where `[AgentHandoffsTo]` requires the user to mark the initial agent via the generic type parameter on `CreateHandoffWorkflow<TInitialAgent>()`. Explicit entry points also serve as documentation.

### LLM Routing as the Default

- **Description**: Default to LLM-driven routing where the agent's model decides which edge to follow based on condition strings as tool descriptions.
- **Rejection Reason**: DAG edges encode orchestration control flow. Nondeterministic routing by default undermines testability, replayability, and determinism. Teams that need deterministic pipelines (compliance, financial, safety-critical) would be forced to opt out of the default. Deterministic-default with LLM opt-in respects both camps.

### Graph-Wide Failure Mode Instead of Per-Edge

- **Description**: Provide a single `FailureMode` property on `[AgentGraphEntry]` with `FailFast` and `ContinueParallel` options, rather than per-edge `IsRequired` semantics.
- **Rejection Reason**: Graph-wide modes are too coarse. A research pipeline may have a required web-research branch and an optional sentiment-enrichment branch. `FailFast` would discard completed research if sentiment fails. `ContinueParallel` would silently ignore failures in the required branch. Per-edge semantics let users express "this branch is optional" without sacrificing safety for required paths.

### Defer Entirely Until MAF Stabilizes

- **Description**: Wait for MAF's graph API to mature beyond v1.1.0 before adding Needlr support, avoiding rework if the API changes.
- **Rejection Reason**: Users need DAG support now for multi-agent research pipelines, content generation workflows, and conditional processing. The attribute model provides an insulation layer — if MAF's underlying API changes, only the factory/generator internals need updating, not the user-facing attribute surface. The risk of rework is contained.

### Separate Package for Graph Workflows

- **Description**: Create a new `NexusLabs.Needlr.AgentFramework.Graph` package rather than adding graph support to the existing `AgentFramework`, `Generators`, and `Analyzers` projects.
- **Rejection Reason**: The three existing workflow types live in the core `AgentFramework` package with shared infrastructure (`IWorkflowFactory`, `WorkflowFactory`, generator, analyzers). A separate package would duplicate shared types, require cross-package analyzer coordination, and fragment the developer experience. Graph workflows should be a first-class citizen alongside the other three types.

## Implementation Notes

- **IMP-001**: New attributes (`AgentGraphEdgeAttribute`, `AgentGraphEntryAttribute`, `AgentGraphReducerAttribute`) should follow the established pattern: `[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]`, placed in `src/NexusLabs.Needlr.AgentFramework/`.
- **IMP-002**: The source generator requires new model types (`GraphEdgeEntry`, `GraphEntryEntry`) in `Generators/Models/` and a new emitter (`GraphCodeGenerator`) in `Generators/CodeGen/`, following the existing `HandoffEntry`/`GroupChatEntry`/`SequenceEntry` pattern.
- **IMP-003**: Analyzer IDs `NDLRMAF016`–`NDLRMAF023` must be registered in `MafDiagnosticIds.cs` and `MafDiagnosticDescriptors.cs`. Cycle detection (`NDLRMAF016`) requires a topological sort or DFS-based algorithm operating on the Roslyn syntax/semantic model.
- **IMP-004**: The `WorkflowFactory` needs a new code path for graph construction that maps `[AgentGraphEdge]` topology to MAF's `WorkflowBuilder` API, translating routing modes to the appropriate edge data types (`DirectEdgeData`, `FanOutEdgeData`, `FanInEdgeData`).
- **IMP-005**: Success criteria — the feature is correct when: (a) a DAG declared entirely via attributes compiles, runs, and produces the expected output; (b) all 8 analyzers fire on invalid topologies with no false positives on valid ones; (c) `IDagRunResult` captures per-node diagnostics with timing and token usage; (d) existing workflow types are unaffected (no regressions in handoff, group chat, or sequential tests).

## References

- **REF-001**: [MAF graph API source (v1.1.0)](https://github.com/microsoft/agent-framework/tree/main/dotnet/src/Microsoft.Agents.AI.Workflows) — `WorkflowBuilder`, `Edge`, `Executor`, `FunctionExecutor<T>`, `DirectEdgeData`, `FanOutEdgeData`, `FanInEdgeData`
- **REF-002**: Existing workflow attributes — `src/NexusLabs.Needlr.AgentFramework/AgentHandoffsToAttribute.cs`, `AgentGroupChatMemberAttribute.cs`, `AgentSequenceMemberAttribute.cs`
- **REF-003**: Existing workflow factory — `src/NexusLabs.Needlr.AgentFramework/IWorkflowFactory.cs`, `WorkflowFactory.cs`
- **REF-004**: Existing source generator — `src/NexusLabs.Needlr.AgentFramework.Generators/AgentFrameworkFunctionRegistryGenerator.cs`, `CodeGen/`, `Models/`
- **REF-005**: Existing analyzers — `src/NexusLabs.Needlr.AgentFramework.Analyzers/MafDiagnosticIds.cs` (IDs `NDLRMAF001`–`NDLRMAF015`)
- **REF-006**: Progress infrastructure — `src/NexusLabs.Needlr.AgentFramework/Progress/IProgressReporter.cs`, `IProgressEvent.cs`
- **REF-007**: Diagnostics infrastructure — `src/NexusLabs.Needlr.AgentFramework/Diagnostics/IPipelineRunResult.cs`, `IAgentStageResult.cs`
- **REF-008**: MAF package version — `src/Directory.Packages.props` pins `Microsoft.Agents.AI.Workflows` at `1.1.0`
