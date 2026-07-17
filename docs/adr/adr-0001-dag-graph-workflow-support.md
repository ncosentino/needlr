---
title: "ADR-0001: DAG/Graph Workflow Support"
status: "Accepted"
date: "2025-07-18"
authors: "Nick Cosentino"
tags: ["architecture", "decision", "agent-framework", "workflows", "dag", "maf"]
supersedes: ""
superseded_by: ""
---

## Context and scope

Needlr's Agent Framework originally supported handoff, group-chat, and sequential
workflows through a consistent model: attributes declare topology, source generation
emits registration and factory code, and analyzers reject invalid declarations before
runtime.

Microsoft Agent Framework (MAF) also supports graph execution for conditional routing,
fan-out, and fan-in. Without a Needlr graph abstraction, consumers that need non-linear
workflows must use raw MAF APIs and give up Needlr's source-generated discovery,
compile-time validation, dependency-injection integration, and common diagnostics.

This decision governs finite directed acyclic workflows composed of Needlr agents and
deterministic reducer nodes. It does not make Needlr a general-purpose distributed
workflow engine, add cyclic execution, or guarantee that every graph can run through the
same MAF execution primitive.

## Decision drivers

- Graph workflows should follow the same attribute, generator, analyzer, and runtime
  composition model as existing Needlr workflows.
- Deterministic and replayable control flow should be the default.
- Routing, joining, and failure semantics must be explicit where topology alone is
  ambiguous.
- Invalid topology and callback signatures should fail at compile time where possible.
- The public model should insulate consumers from changes in MAF's graph API.
- Graph support should reuse existing Agent Framework packages, diagnostics, and progress
  infrastructure rather than create a parallel subsystem.

## Decision

Needlr will support graph workflows as a first-class Agent Framework workflow type.

Graph topology is declared through:

- `AgentGraphEntryAttribute` for an explicit named entry point;
- `AgentGraphEdgeAttribute` for source-to-target edges and per-edge failure requirements;
- `AgentGraphNodeAttribute` for target-side join behavior;
- `AgentGraphReducerAttribute` for deterministic fan-in aggregation.

The source generator emits topology metadata, registration, and execution extensions
through the same surfaces used by other workflow types. Generated topology metadata is the
intended discovery source for trimmed and AOT execution; reflection may exist only as an
explicit fallback. Roslyn analyzers validate cycles, entry points, reachability, edge
targets, terminal nodes, routing callbacks, reducer callbacks, and executor
incompatibilities.

Routing is deterministic by default. `GraphRoutingMode` supports predicate-based
`Deterministic` routing, parallel `AllMatching`, ordered `FirstMatching`,
single-selection `ExclusiveChoice`, and explicit LLM-selected `LlmChoice`. The entry
attribute supplies the graph-wide default, and an edge may declare the effective mode for
its source node. Fan-out routing behavior and fan-in join behavior are declared
independently because multiple outgoing or incoming edges do not fully describe execution
semantics.

`IGraphWorkflowRunner.RunGraphAsync` is the canonical execution entry point. It may use
MAF's native bulk-synchronous execution for compatible `WaitAll` graphs and a
Needlr-owned executor for semantics that MAF does not provide, including `WaitAny` and
LLM-selected routing. Raw MAF workflow creation remains available for compatible graphs
and direct MAF integration.

Failure requirements are declared per edge. Required branch failure fails the graph;
optional branch failure degrades that branch while preserving completed work. Graph
results extend the existing pipeline result model with node and branch diagnostics rather
than introducing an unrelated result hierarchy.

User-facing content comes from terminal graph output. Intermediate node execution is
reported through diagnostics and progress events rather than being treated as an
independent user-facing content stream.

Graph support remains in the existing Agent Framework, Workflows, Generators, and
Analyzers packages. It will not be split into a separate graph package.

## Alternatives considered

### Require consumers to use raw MAF graph APIs

This has the lowest Needlr maintenance cost and exposes every MAF capability immediately.
It was rejected because graph users would lose the compile-time discovery, analyzers,
DI composition, diagnostics, and stable abstraction that justify Needlr's other workflow
types.

### Infer entry, routing, and join semantics from topology

Inference makes simple graphs terse, but multiple roots, multiple outgoing edges, and
multiple incoming edges have several valid meanings. It was rejected because hidden
inference would make behavior difficult to review and could change when topology changes.

### Make LLM-selected routing the default

LLM routing is expressive and avoids writing predicates. It was rejected as the default
because orchestration control flow would become nondeterministic, harder to test, and
harder to audit. It remains available as an explicit mode.

### Use one graph-wide failure mode

A single fail-fast or continue-on-error setting is simpler than per-edge requirements.
It was rejected because real graphs commonly contain both required and optional branches;
one global setting either discards useful completed work or hides failures on required
paths.

### Create a separate graph-workflow package

Package separation could isolate graph-specific dependencies. It was rejected because the
feature shares the same public agents, factory, generator, analyzer IDs, diagnostics, and
composition root as the existing workflow types. A new package would fragment that model.

## Consequences

### Positive

- Consumers can express non-linear workflows without leaving Needlr's source-generated
  and analyzer-backed model.
- Deterministic routing remains the normal path, while LLM routing is available when its
  nondeterminism is intentional.
- Compile-time topology validation catches many graph defects before execution.
- Per-edge failure requirements preserve completed optional work without weakening
  required paths.
- Needlr can adapt its runtime integration when MAF changes without replacing the
  consumer-facing topology model.

### Negative

- Needlr owns a larger public surface for routing, joining, reducers, graph diagnostics,
  and analyzer behavior.
- Two execution strategies must remain behaviorally coherent even though MAF does not
  support every Needlr graph semantic.
- Users must understand the difference between source-side routing and target-side join
  behavior.
- The abstraction is intentionally narrower than a general workflow engine and will not
  represent every arbitrary computation graph.

### Neutral

- Raw MAF workflows remain available for callers that need direct framework control.
- Existing handoff, group-chat, and sequential workflow contracts remain separate and
  unchanged.

## Confirmation

The repository confirms this decision through the following durable boundaries:

- The Agent Framework exposes graph entry, edge, node, reducer, routing, and join types.
- `AgentFrameworkFunctionRegistryGenerator` emits graph topology and execution extensions,
  demonstrating that graph discovery participates in the source-generated model.
- The Agent Framework analyzers include graph topology and callback validation rules.
- `GraphWorkflowRunner` selects between MAF and Needlr execution based on the declared
  topology, while `IGraphWorkflowRunner` exposes one public execution contract.
- Graph runtime, generator, and analyzer tests exercise valid and invalid topologies.
- `GraphWorkflowApp` and the graph section of `docs/ai-integrations.md` demonstrate the
  supported public workflow.

The runtime topology provider currently reconstructs graph metadata through assembly
reflection instead of consuming the generated topology registration. The public graph
workflow is implemented, but the intended AOT-safe discovery path is therefore not fully
confirmed and remains architecture drift against this decision.

Static repository inspection cannot establish production latency, model-routing quality,
or suitability for a particular workload. Those remain operational concerns.

## References

- [`docs/ai-integrations.md`](../ai-integrations.md) documents the public graph attributes,
  execution modes, diagnostics, and runnable example that implement this decision.
- [`IGraphWorkflowRunner` at the confirmation snapshot](https://github.com/ncosentino/needlr/blob/78b27a1c5eddd5b8fa3e0c07dce629159b39e436/src/NexusLabs.Needlr.AgentFramework/IGraphWorkflowRunner.cs)
  is the stable public execution boundary selected by this decision.
- [Microsoft Agent Framework workflows](https://learn.microsoft.com/en-us/agent-framework/workflows/)
  describe the underlying framework capability that Needlr adapts rather than exposes as
  its public topology model.
