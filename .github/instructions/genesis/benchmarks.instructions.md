---
applyTo: "**/*Benchmark*/**/*.cs,**/*Benchmark*.cs"
---

# BenchmarkDotNet

- A `[Benchmark]` method contains only the production call under test and the return/
  `Consumer` needed to prevent dead-code elimination.
- Build inputs/state in `[GlobalSetup]`; use `[IterationSetup]` only when mutation
  requires fresh per-iteration state.
- Call real production code through a project reference. Never recreate an
  implementation inside the benchmark.
- Comparisons use separate methods in one class/run, one marked
  `[Benchmark(Baseline = true)]`, with the same input and consumption pattern.
- Do not select strategies through a `[Params]` enum and branch inside timed code.
- Correctness tests outside the benchmark prove equivalent results.
- Use `[MemoryDiagnoser]`, representative parameter ranges, and host-matching GC/runtime
  mode.
- Do not benchmark real network/disk/database I/O unless that I/O is the subject.
- Retire the comparison when the losing implementation is removed, unless both remain
  supported product strategies.
