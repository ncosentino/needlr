---
# AUTO-GENERATED from .github/instructions/genesis/react-spa/fitness-scripts.instructions.md — do not edit
paths:
  - "**/scripts/**/*.mjs"
---
# Fitness Function Script Rules

These rules apply to ESM helper scripts under `scripts/`. They cover
the conventions for "fitness functions" — scripts that walk the
source tree, assert an architectural or quality invariant, and gate
the build by exit code.

## Parse with an AST, never regex over source code

Source analysis MUST use an AST parser (e.g. `@babel/parser`,
`acorn`, `typescript-eslint/typescript-estree`). Never `grep`-style
regex over source files. Source code is full of edge cases —
string literals containing keywords, comments, multi-line constructs,
template strings, JSX — that silently slip past regex and produce
wrong answers.

```mjs
// ❌ WRONG — regex catches the literal "import" inside a string;
// misses dynamic import(), re-exports, type-only imports, etc.
const imports = code.match(/^import .* from ['"](.+)['"]/gm);

// ✅ CORRECT — AST walk finds every real import declaration
import { parse } from '@babel/parser';
const ast = parse(code, { sourceType: 'module', plugins: ['typescript', 'jsx'] });
// walk ast.program.body for ImportDeclaration / ExportNamedDeclaration /
// ExportAllDeclaration / CallExpression with callee.type === 'Import'
```

## Exit codes

- `0` on success, with a single-line summary on stdout
  (e.g. `check-foo: scanned 247 file(s); 0 violations.`).
- `1` on any violation, with structured per-violation output on stderr
  before the summary line.

Never exit `0` with violations printed as warnings — CI gates on the
exit code, not on string matching.

## Structured per-violation output

Each violation includes: relative file path, line number, violation
kind, and a human-readable message. Keep the format stable so editor
integrations and CI parsers can rely on it.

```
  src/features/foo/components/Bar.tsx:42
    feature "foo" imports from sibling feature "baz"
    import: ../../baz/components/Baz
```

## Separate the check function from the CLI entrypoint

Export the check function (`export function checkXxx(root) { ... }`)
so it can be unit-tested in isolation against fixture directories.
The CLI entrypoint at the bottom of the file is a thin wrapper:

```mjs
const isCli = import.meta.url === `file://${process.argv[1]}` ||
              import.meta.url.endsWith(toPosix(process.argv[1] ?? ''));

if (isCli) {
  const { violations, scanned } = checkXxx();
  if (violations.length > 0) {
    console.error(`check-xxx: ${violations.length} violation(s) in ${scanned} file(s):`);
    for (const v of violations) console.error(formatViolation(v));
    process.exit(1);
  }
  console.log(`check-xxx: scanned ${scanned} file(s); 0 violations.`);
  process.exit(0);
}
```

A test file at `scripts/check-xxx.test.mjs` then imports `checkXxx`
and runs it against fixture trees without spawning a subprocess.
