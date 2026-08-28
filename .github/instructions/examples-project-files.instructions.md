---
applyTo: "**/Examples/**/*.csproj"
---

# Example Project Files

- Keep examples on the repository-supported .NET 10 target line.
- Generator project references use `OutputItemType="Analyzer"` and
  `ReferenceOutputAssembly="false"`.
- Register new examples in `src/NexusLabs.Needlr.slnx` under the appropriate
  `/Examples/` folder.
- Copy `appsettings.json` with `PreserveNewest` when an example reads it at runtime.
