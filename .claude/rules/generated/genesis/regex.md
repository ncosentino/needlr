---
# AUTO-GENERATED from .github/instructions/genesis/regex.instructions.md — do not edit
paths:
  - "**/*.cs"
---
# Regular Expression Rules

- Use `[GeneratedRegex]` for patterns known at compile time instead of `RegexOptions.Compiled`; it
  avoids runtime code generation and remains compatible with NativeAOT.
- Regexes that process untrusted input or use runtime-supplied patterns must use a finite match
  timeout, including generated regexes when applicable. Treat `RegexMatchTimeoutException` as an
  explicit failure; never rely on `Regex.InfiniteMatchTimeout`.
