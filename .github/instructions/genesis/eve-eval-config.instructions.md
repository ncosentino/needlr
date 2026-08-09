---
applyTo: "**/evals/evals.config.ts"
---

# Eve Eval Configuration Rules

Eve requires exactly one `evals/evals.config.ts` at the application root. Default-export a
`defineEvalConfig(...)` value.

- Configure only run-wide defaults: the optional judge model, reporters, bounded
  concurrency, and timeout. Per-eval and CLI settings remain the narrower overrides.
- Keep the judge configuration separate from the subject agent configuration. Pin the
  comparison judge; prefer a different model or family when practical.
- If the same model or family is intentionally used for subject and judge, document the
  correlated-evaluator limitation and require human-labelled calibration evidence before
  trusting semantic scores.
- Keep score thresholds on individual Eve assertions, not in a detached configuration map.
- When a helper constructs a custom judge model instance, surface resolution or credential
  failures explicitly and test how the eval run classifies them. Do not assume missing
  judge credentials produce a skipped eval.
