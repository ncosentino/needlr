---
# AUTO-GENERATED from .github/instructions/analyzers.instructions.md — do not edit
paths:
  - "**/*Analyzer.cs"
  - "**/DiagnosticDescriptors.cs"
  - "**/AnalyzerReleases.*.md"
---
# Analyzer Rules

## Diagnostic ID convention

IDs use the `NDLR` prefix plus a component code:

| Component | Prefix | Example |
|-----------|--------|---------|
| Core | `NDLRCOR` | `NDLRCOR001` |
| Generators | `NDLRGEN` | `NDLRGEN014` |
| Agent Framework | `NDLRMAF` | `NDLRMAF001` |
| SignalR | `NDLRSIG` | `NDLRSIG001` |
| HttpClient | `NDLRHTTP` | `NDLRHTTP001` |

## Release tracking (RS2000)

Every new diagnostic MUST be added to `AnalyzerReleases.Unshipped.md` in the same project. Format:

```
NDLRXXX | NexusLabs.Needlr.Generators | Error | AnalyzerClassName, Short title
```

Forgetting this causes `RS2000` build errors.

## Message format (RS1032)

- **Single sentence**: no trailing period. Example: `"Type '{0}' has [HttpClientOptions] but no name source resolves to a non-empty value"`
- **Multi-sentence**: trailing period on the last sentence. Example: `"Type '{0}' has [HttpClientOptions(Name = \"{1}\")]. Pick one source and remove the other."`

## Compilation-end diagnostics (RS1037)

If a diagnostic is reported from a `RegisterCompilationEndAction`, the descriptor MUST include `customTags: WellKnownDiagnosticTags.CompilationEnd`.

## Analyzer class shape

```csharp
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MyAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ...;
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(...) or RegisterSymbolAction(...);
    }
}
```

## Documentation requirements

Every new diagnostic needs ALL THREE:
1. A dedicated `docs/analyzers/NDLRXXX.md` page (sections: Cause, Rule Description, How to Fix with before/after code, See Also)
2. A nav entry in `mkdocs.yml` under the appropriate Analyzers subgroup
3. A row in `docs/analyzers/README.md`
