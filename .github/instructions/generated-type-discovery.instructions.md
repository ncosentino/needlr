---
applyTo: "**/*DiscoveryHelper.cs,**/TypeRegistryGenerator.cs,**/CodeGen/FactoryCodeGenerator.cs"
---

# Same-compilation generated dependencies

- Discovery must recognize dependency types Needlr itself emits in the same compilation.
  Reuse the actual generation model and declaration emitter; do not infer generated
  contracts from naming conventions or accept arbitrary unresolved symbols.
- Preserve normal C# namespace/alias binding, generation modes, and discovery filters.
- Add red-green tests resolving the consuming service through real generated DI.
  Factory resolution, generated-source compilation, and manual registrations alone
  do not prove consumer discovery. Keep constructor metadata and manifest parity covered.
