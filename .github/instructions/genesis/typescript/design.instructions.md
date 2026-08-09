---
applyTo: "**/*.ts,**/*.tsx"
---

# TypeScript Design Rules

- Prefer composition and pure domain functions over inheritance. Use classes when
  required by a framework or when they clearly encapsulate cohesive stateful behavior.
- Prefer string-literal unions or `as const` objects for application-owned states.
  Enums remain appropriate for generated code, external contracts, numeric flags, or
  interoperability.
- Prefer arrow functions for callbacks and closures. Function declarations are
  appropriate for exported helpers, components, recursion, hoisting, and overloads.
- Handle discriminated unions exhaustively. Use an exhaustive `switch`, explicit
  guards, or a pattern-matching library appropriate to the project.
- Represent expected domain failures explicitly when callers must branch on the
  outcome. Use exceptions for programming errors and APIs whose established contract
  is exception-based.
- Represent absence explicitly using `T | undefined`, nullable unions, or an
  `Option<T>` abstraction appropriate to the project.
- Do not rewrite generated or vendored code solely to enforce these preferences.
