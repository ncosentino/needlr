# .NET project and feature structure

Needlr is a package family rather than one application. Project boundaries separate
runtime abstractions, discovery strategies, compile-time tooling, and optional
framework integrations.

## Main project groups

- `NexusLabs.Needlr*` contains the fluent composition API and shared runtime behavior.
- `NexusLabs.Needlr.Injection.*` contains source-generated, reflection, Scrutor, and
  bundle discovery strategies.
- `NexusLabs.Needlr.Generators.Attributes`, `NexusLabs.Needlr.Generators`,
  `NexusLabs.Needlr.Analyzers`, and `NexusLabs.Needlr.Build` own compile-time behavior.
- Integration packages such as Carter, SignalR, Hosting, Logging, Serilog,
  FluentValidation, Avalonia, and MAUI remain independently consumable.
- Test, integration-test, example, and benchmark projects verify the corresponding
  package boundary through real composition.

The solution file at `src/NexusLabs.Needlr.slnx` is the authoritative project graph.
IDE extensions have a separate solution and SDK pin under `ide-extensions/`.

## Central package versions

`Directory.Packages.props` owns versions. Project files reference package names without
inline `Version` attributes so one dependency version governs the package family.

## Source-generated features

A source-generated feature spans its user-facing attribute, Roslyn discovery helper,
metadata model, code generator, analyzer, integration tests, and documentation. Do not
add only the emitted code path while omitting its diagnostics or runtime proof.
