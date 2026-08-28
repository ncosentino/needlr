---
# AUTO-GENERATED from .github/instructions/genesis/react-spa/vitest-coverage.instructions.md — do not edit
paths:
  - "**/{vite,vitest}.config.{ts,js,mjs}"
---
# Vitest Coverage Rules

These rules apply to any Vitest configuration file, whether the config
lives in its own `vitest.config.*` or is combined into a `vite.config.*`
via the `test` key.

## Per-file thresholds, not repo-wide averages

`coverage.thresholds.perFile` MUST be `true`. A repo-wide average lets
one well-tested file mask a totally uncovered one — coverage gates the
average, not the gap. Per-file enforcement surfaces the gap where it
actually is.

```ts
// ❌ WRONG — repo-wide average; one uncovered file can hide behind dozens
coverage: {
  thresholds: {
    lines: 85,
    branches: 80,
    functions: 85,
    statements: 85,
  },
}

// ✅ CORRECT — every individual file must clear the floor
coverage: {
  thresholds: {
    lines: 85,
    branches: 80,
    functions: 85,
    statements: 85,
    perFile: true,
  },
}
```

## Recommended starting floors

- `lines: 85`
- `statements: 85`
- `functions: 85`
- `branches: 80`

Adjust upward as the codebase matures, not downward. Lowering a floor
to fit reality means you're trading a gate for a placebo.

## Legitimate exclusions

Files that can't meaningfully be unit-tested (entry point that just
calls `createRoot`, vite/vitest config itself, browser-only mock
modules, type-declaration files) belong in `coverage.exclude`, not in
a lowered threshold. The threshold stays honest; the exclusion list
documents why something was carved out.
