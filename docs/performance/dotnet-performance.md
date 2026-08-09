# .NET performance

Needlr's primary performance boundary is discovery and container construction.
Source generation moves discovery work to compile time; reflection remains an explicit
runtime option for dynamic scenarios.

## Measure first

Use the BenchmarkDotNet project under `src/NexusLabs.Needlr.Benchmarks` and compare
production code paths in the same run. Do not publish performance claims from an
untracked local stopwatch or from different machines/configurations.

The [Benchmarks](../benchmarks.md) page is populated by the scheduled benchmark
workflow and records the execution environment.

## Generator performance

Incremental generators should:

- keep pipeline inputs equatable and deterministic;
- avoid collecting the full compilation when a narrower provider is sufficient;
- avoid repeated symbol traversal and string construction;
- emit stable source so unchanged inputs remain cached.

Performance changes must preserve analyzer behavior, generated-code correctness, and
cross-assembly discovery.

## Runtime and AOT

Both Native AOT example applications are protected CI gates. A change that improves a
JIT benchmark but introduces reflection, trimming warnings, nondeterminism, or extra
runtime registration work is not an acceptable optimization.
