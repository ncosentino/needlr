---
description: Compile, test, and pack NexusLabs.Needlr from source.
---

# Building from Source

You need the [.NET SDK](https://dotnet.microsoft.com/download) installed. Check
`global.json` in the repository root for the exact version this project targets.

## Build

```bash
dotnet restore src/NexusLabs.Needlr.slnx
dotnet build src/NexusLabs.Needlr.slnx --configuration Release --no-restore
```

## Test

```bash
dotnet test src/NexusLabs.Needlr.slnx --configuration Release --no-build
```

Use a targeted test project or filter while iterating. Pull-request CI owns the full
package, documentation, integration, and Native AOT gates.

## Documentation

```bash
python -m pip install -r docs/requirements.txt
python -m mkdocs build --strict
```
