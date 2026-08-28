---
# AUTO-GENERATED from .github/instructions/genesis/nextjs-runtime.instructions.md — do not edit
paths:
  - "**/next.config.{js,mjs,ts}"
  - "**/app/**/route.{js,ts}"
  - "**/app/**/{page,layout}.{js,jsx,ts,tsx}"
---
# Next.js runtime boundaries

Determine the runtime contract from `next.config.*` before adding server features:

- `output: 'export'` is a static product. Do not add Route Handlers, Server Actions,
  request-time rendering, authenticated server integrations, or other runtime-only features.
- Without static export, App Router pages/layouts are Server Components by default and Route
  Handlers run on the selected server runtime.

Keep client boundaries narrow:

- Add `'use client'` only for state, event handlers, effects, context, or browser APIs.
- Never import a `server-only` module into a Client Component.
- Non-`NEXT_PUBLIC_` environment values stay server-only; `NEXT_PUBLIC_` values are inlined
  into browser bundles at build time.
- Validate server environment values at one server boundary and return explicit failures rather
  than allowing unchecked values to spread through the application.

Route Handlers should use Web `Request`/`Response` contracts, declare runtime/caching behavior
when it matters, and have deterministic tests that invoke the exported handler directly when no
live provider is required.
