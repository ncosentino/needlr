# Testing and benchmarks

Needlr uses deterministic tests to protect behavior and BenchmarkDotNet measurements
to evaluate performance. Tests remain the correctness authority; benchmarks compare
equivalent production paths after correctness is established.

## Benchmark harnesses

Benchmark methods contain only the production call under test. Setup, data generation,
validation, logging, and assertions live outside timed code.

Comparisons run baseline and candidate in the same class/run with the same input and
consumption pattern. A strategy switch inside one benchmark measures the branch as
well as the strategy and is not a valid comparison.

Benchmarks call real production code through a project reference. Correctness tests in
the normal test project prove equivalent outcomes.

Use memory diagnostics and representative parameter ranges. The scheduled workflow
publishes machine/runtime metadata with results; compare measurements only when the
environment and benchmark definition are equivalent.

The public [Benchmarks](../benchmarks.md) page is the canonical published result.
