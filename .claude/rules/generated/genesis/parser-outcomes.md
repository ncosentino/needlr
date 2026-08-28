---
# AUTO-GENERATED from .github/instructions/genesis/parser-outcomes.instructions.md — do not edit
paths:
  - "**/*Parser.cs"
  - "**/*Reader.cs"
  - "**/*Tokenizer.cs"
  - "**/*Lexer.cs"
  - "**/Parsing/**/*.cs"
  - "**/Parsers/**/*.cs"
---
# Parser Outcome and Speculative Probe Rules

Apply these rules when the matching file parses or speculatively probes structured
input. An ordinary I/O reader that does not interpret input is outside this
instruction's scope.

- Model expected outcomes explicitly, preserving distinctions such as success,
  no-match, incomplete input, invalid input, unsupported input, and budget exceeded
  whenever callers respond differently to them.
- Do not use a deliberately thrown exception as a parser branch, backtracking signal,
  or probe result.
- A speculative probe must either commit or restore every input position, buffered
  value, diagnostic, budget counter, and terminal disposition it was allowed to
  change.
- A failed probe must not consume input or leak provisional diagnostics unless that is
  part of its documented result contract.
- Prefer immutable/local probe state or an explicit snapshot-and-restore boundary over
  manually reversing individual mutations.
- Catch exception-based framework parsers only at the narrow conversion boundary and
  translate malformed input into the explicit parser outcome.
