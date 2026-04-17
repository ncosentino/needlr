---
applyTo: "**/*Analyzer.cs,**/DiagnosticDescriptors.cs,**/AnalyzerReleases.*.md"
---

# Roslyn Analyzer Rules

## Diagnostic ID convention

Choose a short project-level prefix (e.g., `MYAPP`) and assign component codes to group related diagnostics:

| Component | Prefix | Example |
|-----------|--------|---------|
| Core | `MYAPPCOR` | `MYAPPCOR001` |
| Generators | `MYAPPGEN` | `MYAPPGEN001` |

Maintain sequential numbering within each component. Never reuse a retired ID.

## Release tracking (RS2000)

Every new diagnostic MUST be added to `AnalyzerReleases.Unshipped.md` in the same project. Format:

```
MYAPPXXX | MyApp.Analyzers | Error | AnalyzerClassName, Short title
```

Forgetting this causes `RS2000` build errors.

## Message format (RS1032)

- **Single sentence**: no trailing period. Example: `"Type '{0}' must implement IDisposable"`
- **Multi-sentence**: trailing period on the last sentence. Example: `"Type '{0}' has conflicting attributes. Remove one of them."`

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

- Always `public sealed class`
- Always call `ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None)` — analyzers should not run on generated code
- Always call `EnableConcurrentExecution()` for performance

## Suppressing analyzer warnings

`[System.Diagnostics.CodeAnalysis.SuppressMessage]` is **STRICTLY FORBIDDEN** without explicit team lead approval. Never suppress analyzer warnings to make code compile. Fix the code instead.
