# .NET engineering conventions

Needlr favors analyzable, deterministic code because much of its behavior executes at
compile time and must remain compatible with trimming and Native AOT.

## Build and package shape

- The root `global.json` pins the SDK used by the main solution.
- `src/Directory.Packages.props` owns NuGet versions.
- Public APIs require XML documentation.
- One type per file keeps Roslyn discovery and ownership predictable.
- Types that are instantiable but are not services use `[DoNotAutoRegister]`.

## Source generation

Generator and analyzer assemblies target their required Roslyn-compatible framework.
Generated output must be deterministic, compile for the supported language version,
and avoid reflection unless the consumer explicitly selects a reflection package.

Every new generated feature includes analyzer coverage and a real integration test that
builds a `Syringe` service provider and exercises the emitted registration.

## Tests

Test projects use xUnit v3 through central configuration. Run the narrow project or
filter that proves a change while iterating; the complete solution, packaging, docs,
and Native AOT gates run through the PR workflow.

## Logging and time

Use generated logging where the package exposes repeated structured events. Inject
`TimeProvider` for controllable time instead of reading the system clock directly.
Neither concern should introduce static singleton state into a dependency-injection
library.
