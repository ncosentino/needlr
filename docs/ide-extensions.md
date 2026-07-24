---
description: Configure Needlr dependency-graph export for the VS Code and Visual Studio extensions.
---

# IDE Extensions

Needlr's VS Code and Visual Studio extensions consume `needlr-graph.json` or the
equivalent JSON embedded in `NeedlrGraph.g.cs`. They provide service browsing,
dependency visualization, CodeLens, and source navigation.

## Enable Graph Export Solution-Wide

Each project owns source locations for the types and interfaces it compiles. Enable graph
export in a solution-level `Directory.Build.props` so every participating project emits
its own location-rich graph:

```xml
<Project>
  <PropertyGroup>
    <NeedlrExportGraph>true</NeedlrExportGraph>
  </PropertyGroup>
</Project>
```

A project-level property also works, but navigation into referenced projects requires
those projects to emit their own graphs.

## How Workspace Merging Works

The extensions search for graph files from every project in the open workspace and merge
them by fully qualified type name:

1. Referenced service and dependency structure can appear in a consuming project's graph.
2. The producer project's own graph supplies its service and interface source locations.
3. A location-rich producer entry replaces a locationless referenced entry.
4. Interface locations collected from any producer graph are backfilled onto matching
   entries throughout the merged graph.

This keeps source navigation local and accurate without loading referenced assemblies or
duplicating source-location metadata into every downstream project.

## Compiled References

Normal command-line project references and NuGet dependencies are PE metadata references.
Roslyn exposes their type structure but not declaring syntax, so their locations are
reported as `null` in a consuming project's graph.

For projects in the same workspace, the producer graph restores those locations during
IDE merging. External package services remain visible, but source navigation is
unavailable unless the source project is also present.

Package navigation would require a separate SourceLink-aware design. Needlr does not use
paths captured on a package author's build machine as local navigation targets.

## Graph Files

The extensions discover:

1. `**/obj/**/needlr-graph.json`
2. `**/NeedlrGraph.g.cs`

The shared contract remains
[`schemas/needlr-graph-v1.schema.json`](https://github.com/ncosentino/needlr/blob/main/schemas/needlr-graph-v1.schema.json).
The ownership model does not change schema version `1.0`.

## Architecture

See [ADR-0007](adr/adr-0007-own-graph-source-locations-per-project.md) for the
cross-project source-location decision.
