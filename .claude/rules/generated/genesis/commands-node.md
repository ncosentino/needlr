---
# AUTO-GENERATED from .github/instructions/genesis/commands-node.instructions.md — do not edit
paths:
  - "**/package.json"
  - "**/*.ts"
  - "**/*.tsx"
---
# Node / TypeScript Commands & Toolchain

## Install dependencies

```sh
npm install
```

## Run the dev server

```sh
npm run dev
```

## Build for production

```sh
npm run build
```

## Quality gate

```sh
npm test
```

`npm test` runs the full quality wall: `npm run lint` (ESLint), then `npm run format:check`
(Prettier), then the test runner (e.g. `vitest run`). If any stage fails, the gate fails. Before
committing, run `npm install && npm run build && npm test` and ensure all of it exits 0.

## Conventions

- TypeScript strict mode is on. Do not loosen it.
- ESLint and Prettier are part of the test gate. Do not bypass them with inline disable comments
  unless absolutely necessary, and explain why if you do.
- ESLint and Prettier honor `.gitignore` in addition to their own ignore files (`.prettierignore`,
  the flat `eslint.config.js`). Add a path to `.gitignore` once and it is ignored by lint and
  format:check too — no need to duplicate it. The tool-specific ignore files remain the override
  surface for tracked files that should still be skipped (e.g. an auto-generated `README.md`).
  Editor-integrated Prettier may need separate configuration to honor `.gitignore`; the CLI gate
  (`npm test`) always does.
- With Vite, `*.module.css` is type-declared in `src/vite-env.d.ts` — use CSS Modules
  (`import styles from './X.module.css'`) for component-scoped styles.
