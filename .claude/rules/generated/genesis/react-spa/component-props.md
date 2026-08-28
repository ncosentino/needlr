---
# AUTO-GENERATED from .github/instructions/genesis/react-spa/component-props.instructions.md — do not edit
paths:
  - "**/*.tsx"
---
# React component props

- Every `Props`/`*Props` field is `readonly`; array contracts use
  `ReadonlyArray<T>` rather than `T[]`.
- Production props contain only values a real caller supplies.
- Do not add test-only controls such as `now`, `forceLoading`, test fetch functions, or
  `__*ForTests` seams. Inject controllable behavior through context providers,
  factories, or DI.
- Prefer role/label queries for accessible UI. When localized or repeated UI needs a
  `data-testid`, keep it independent of rendered text and locale.
- Stable test ids use `<feature-or-component>-<role>[-<dynamic-id>]`; never derive them
  from translated copy.
- Component-local mutation is allowed; the external prop contract remains immutable.
