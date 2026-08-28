---
# AUTO-GENERATED from .github/instructions/genesis/performance/gc-tuning.instructions.md — do not edit
paths:
  - "**/*.csproj"
---
# Garbage Collector Tuning

## Choosing a GC mode

- Default to Server GC (`<ServerGarbageCollection>true</ServerGarbageCollection>`) for
  throughput-oriented services (web APIs, workers, functions).
- Prefer Workstation GC for desktop/CLI apps where UI responsiveness or single-instance memory
  footprint matters more than raw throughput.
- MUST NOT assume Server GC is strictly better when many instances of the app run on the same host
  (e.g. many containers per node) — its per-core GC heaps and threads compete with each other;
  Workstation GC with concurrent GC disabled is the documented recommendation for that scenario.

## Dynamic heap sizing (DATAS)

- Since .NET 9, Server GC adapts heap count/size to actual load by default instead of provisioning
  one heap per core up front. MUST NOT disable DATAS
  (`<System.GC.DynamicAdaptationMode>0</System.GC.DynamicAdaptationMode>`) by default or by habit —
  only disable it after profiling shows its more frequent, smaller collections hurt a specific
  latency-sensitive workload's tail latency.

## GC regions

- If a container hits a virtual-memory limit at startup, cap the GC's upfront reservation via
  `<System.GC.RegionRange>` — the .NET 7+ region-based heap reserves virtual address space ahead of
  use.
