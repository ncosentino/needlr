---
# AUTO-GENERATED from .github/instructions/genesis/vercel-agent-eval-graders.instructions.md — do not edit
paths:
  - "**/evals/**/EVAL.ts"
  - "**/evals/**/EVAL.tsx"
---
# Vercel Agent Eval Grader Rules

`EVAL.ts` or `EVAL.tsx` is the hidden Vitest grader used by Vercel Agent Eval.

## Grade observable behavior

- Verify the final outcome and environment state, not merely the agent's final message.
- Use deterministic assertions for files, content, builds, tests, schemas, and other
  objective outcomes.
- Read `__agent_eval__/results.json` for transcript-derived behavior such as commands,
  files, tools, turns, and errors when that process is part of the contract.
- Do not require exact prose when multiple semantically correct responses are valid.
- Use exact gold answers only for uniquely correct outputs. Otherwise encode observable
  constraints or use a reference answer as calibration context rather than an exact match.

## Agentic model judges

Use `environment` or `transcript` judge assertions only for qualities deterministic
Vitest checks cannot establish:

- Grade one dimension with a concrete rubric and structured result.
- Pin an independent judge model when comparing subjects; do not rely on self-grading.
- Treat the framework's default same-agent/model judge as exploratory. Pin and calibrate a
  stable comparison judge before any semantic assertion controls a gate.
- Keep criteria focused because every judge assertion adds an agent run.
- Use separate judge assertions for independent dimensions or tightly coherent criteria
  groups, and aggregate their results outside the judge. A bundled scorecard may reduce
  agent-run cost but remains diagnostic until it agrees with isolated judges on
  human-labelled cases.
- Require a strict enum, boolean, or bounded numeric result and validate it before scoring.
  Malformed judge output is a grader or infrastructure failure, not an incorrect subject.
- Let the judge reason before deciding when useful, then parse a strict verdict and concise
  cited evidence. Do not depend on provider-native hidden chain-of-thought as the result.
- Maintain executable judge-calibration cases with human-labelled hard, failed, borderline,
  and adversarial examples. Until that calibration is credible, judge assertions remain
  diagnostic rather than release gates.

## Reliable execution

- Keep `EVAL.ts` / `EVAL.tsx` unavailable to the agent under test.
- Keep generated results, transcripts, and reports outside the committed definition tree.
- Preserve separate dimension and slice results; do not reduce a multidimensional suite to
  one unexplained aggregate.
