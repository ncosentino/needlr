---
# AUTO-GENERATED from .github/instructions/genesis/commands-dotnet.instructions.md — do not edit
paths:
  - "**/*.csproj"
  - "**/*.cs"
  - "**/*.slnx"
---
# .NET commands

- Build with `dotnet build`.
- The repository pins Microsoft.Testing.Platform in `global.json`.
- TUnit projects run with `dotnet test`. On .NET 10, run from the solution directory
  or pass `--solution`/`--project`; do not pass a solution as a positional argument.
- A zero-test TUnit project fails with exit code 8. Every new test project includes at
  least one passing test.
- xUnit projects under the MTP-pinned repository run as executables with
  `dotnet run --project <test-project>`; do not remove the runner pin.
- Report exact pass/fail/skip and warning/error counts.
- Complete suites are CI/PitCrew work; local iteration uses the narrow project/gate
  that proves the changed behavior.
