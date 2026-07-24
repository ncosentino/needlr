---
title: "ADR-0007: Own dependency-graph source locations per project"
status: "Accepted"
date: "2026-07-24"
authors: ["Nick Cosentino"]
tags: ["architecture", "decision", "source-generation", "ide", "dependency-graph"]
supersedes: ""
superseded_by: ""
---

## Context and scope

Needlr can emit a dependency graph for IDE tooling. The graph includes services,
interfaces, dependencies, lifetimes, and optional source locations used for CodeLens
placement and go-to-source navigation.

A project being compiled has Roslyn syntax trees for its own types, so its generator can
record accurate source paths and line numbers. Referenced assemblies are different:
normal command-line project references and NuGet dependencies appear as PE metadata.
Their symbols expose type structure but not declaring syntax or source locations.

The existing cross-assembly location fallback attempted to find a generated
`ServiceCatalog.Services` property and parse its C# initializer text. That can only work
when the reference remains a Roslyn `CompilationReference`; PE metadata symbols have no
`DeclaringSyntaxReferences`, so the normal build path cannot provide the text being
parsed.

Needlr's VS Code extension and both Visual Studio extensions already load every graph in
the workspace, merge services by fully qualified type name, prefer entries with source
locations, and backfill interface locations from producer graphs. This establishes a
natural ownership model: each project knows its own locations, while IDE consumers know
how to combine projects.

This decision governs source-location ownership and graph merging for projects in an
open solution. It does not define source navigation into external NuGet packages, which
would require SourceLink-aware tooling rather than build-machine file paths.

## Decision drivers

- Source navigation must use paths that exist in the developer's workspace.
- Graph generation must remain deterministic, reflection-free, and safe for build hosts.
- The graph JSON schema consumed by existing IDE extensions must remain compatible.
- Cross-project behavior should use the architecture already implemented by the IDE
  consumers instead of duplicating producer data into every downstream graph.
- Missing source information must degrade to an explicit null location rather than a
  fabricated or stale path.
- External package navigation must not expose or trust paths from another machine.

## Decision

Each project graph is authoritative for source locations belonging to that project's
assembly.

When discovering services from a referenced assembly:

- Needlr continues to include the referenced service and dependency structure needed for
  graph completeness and cross-assembly dependency resolution.
- A `CompilationReference` may contribute source locations directly from its Roslyn
  symbols.
- A PE metadata reference contributes null source locations because its source is not
  available to the compilation.
- Needlr does not parse generated ServiceCatalog syntax, load referenced assemblies,
  inspect runtime values, read PDB files, or introduce a second metadata protocol solely
  to recover locations.

IDE consumers merge all available project graphs by full type name. The producer
project's location-rich entry takes precedence over a referenced metadata entry without
a location. Interface locations are collected across graphs and backfilled onto every
matching interface entry.

Solutions that use Needlr IDE navigation should enable `NeedlrExportGraph` in a shared
`Directory.Build.props` so every participating project emits its own graph. The existing
graph schema remains at version 1.0 because this decision changes ownership and
generation behavior, not the JSON contract.

External package services remain visible in the dependency graph when discoverable, but
their locations are null unless their source project is also present in the workspace.
Future package navigation, if required, must use a SourceLink-aware design.

## Alternatives considered

### Parse generated ServiceCatalog syntax

This preserves the current fallback for `CompilationReference` scenarios, but it is
ineffective for normal PE references and duplicates source information already available
from the producer graph. Regex parsing generated C# is also coupled to an implementation
format rather than a stable contract. It was rejected.

### Emit a cross-assembly source-location metadata protocol

Assembly attributes or constant manifests could make locations readable from PE
metadata. This would make an individual consumer graph more self-contained, but it would
duplicate location data in every assembly, create a versioned producer/consumer protocol,
and still produce paths that are not navigable for external packages. It was rejected
because existing IDE consumers already merge producer graphs.

### Load ServiceCatalog implementations during generation

Loading referenced assemblies and invoking generated runtime catalogs would recover
structured values, but source generators must not execute arbitrary referenced code or
introduce dependency-load and side-effect risks. It was rejected.

### Read portable PDB and SourceLink information

This could eventually support external package navigation, but PDBs may be absent and the
implementation would add binary I/O, source mapping, caching, and download concerns to
the generator. It is outside the current workspace-graph requirement.

## Consequences

### Positive

- Source locations come from the project that actually owns and can navigate to the
  source.
- Existing VS Code and Visual Studio merge behavior becomes the documented architecture.
- The ineffective syntax parser and its regex-dependent branches are removed.
- Normal PE references degrade predictably without reflection, PDB access, or metadata
  duplication.
- The graph JSON contract remains compatible with existing extensions.

### Negative

- A single graph file is not guaranteed to contain navigable locations for every
  referenced service.
- Navigation requires producer projects to enable graph export and produce graph files.
- External NuGet package source navigation remains unavailable.
- IDE consumers must continue loading and merging multiple graph files.

### Neutral

- Referenced services and cross-assembly dependencies remain in the graph even when their
  locations are null.
- `ServiceCatalog` remains available for runtime introspection; it is no longer treated
  as a compile-time source-location transport.

## Confirmation

Generator tests compile a producer as both a `CompilationReference` and a real PE
metadata reference. The source reference must retain producer locations; the PE reference
must retain graph structure while exposing null locations.

Graph-generation tests verify that a producer's own graph contains its service and
interface source paths. IDE consumer builds and merge-focused tests confirm that
location-rich producer entries replace locationless duplicates and that interface
locations are backfilled without changing schema version 1.0.

Documentation validation confirms that solution-wide graph export is the supported
navigation setup and that external package locations are explicitly unsupported.

## References

- `ide-extensions/vscode/src/graphLoader.ts` loads all project graphs, prefers service
  entries with locations, and backfills interface locations.
- `ide-extensions/visualstudio/NeedlrToolsExtension/GraphLoader.cs` implements the same
  merge contract for the Visual Studio tool window.
- `ide-extensions/visualstudio/NeedlrCodeLens/GraphLoader.cs` merges project graphs for
  Visual Studio CodeLens.
- `schemas/needlr-graph-v1.schema.json` is the stable JSON contract shared by all IDE
  consumers.
