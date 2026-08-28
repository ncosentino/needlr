---
# AUTO-GENERATED from .github/instructions/genesis/workflow-eval-graders.instructions.md — do not edit
paths:
  - "**/*.Tests.Eval/**/*Check.cs"
  - "**/*.Tests.Eval/**/*Evaluator.cs"
---
# Agentic Workflow Eval Grader Rules

Use code-based checks for objective workflow evidence and model evaluators only for
semantic qualities.

## Deterministic checks

Files ending `Check.cs` verify artifacts, metadata, diagnostics, or environment state
without an LLM call.

- Implement one invariant per check.
- Make the check a pure, thread-safe function of explicit inputs.
- Include specific evidence such as counts, identifiers, paths, and quoted values.
- When a check contributes to an aggregate, emit the metric, unit, and denominator needed
  by the suite success contract. A standalone invariant may return pass or fail with
  specific evidence.
- Verify output produced by the workflow, not validity already present in the seed.
- For tool-using workflows, cover unknown or failed tools, unauthorized side effects, and
  unsafe instructions entering through retrieved content when relevant.
- Return a recorded not-applicable or inconclusive result when required evidence is
  absent; do not silently pass.

## Model evaluators

Files ending `Evaluator.cs` judge one semantic quality that deterministic code cannot
establish.

- Use the supplied judge configuration; never construct the subject-under-test client as
  the judge.
- Pass structured evaluator inputs as native objects or serialized data, not ambiguous
  `ToString()` output.
- Require quoted evidence and populate the result's interpretation, failure state, and
  reason.
- Require a strict enum, boolean, or bounded numeric result and validate it before scoring.
  Malformed output is a grader or infrastructure failure, not a failed subject trial.
- Let the judge reason before deciding when useful, then retain a strict verdict and concise
  evidence. Do not depend on provider-native hidden chain-of-thought as the result.
- Treat each model evaluator as a subject with its own executable, human-labelled
  calibration cases before a policy uses its scores to gate.
- Keep independent semantic dimensions in separate evaluator calls or tightly coherent
  groups. Bundled scorecards remain diagnostic until calibrated against isolated evaluators
  and checked for halo and rubric-order effects.
- Keep evaluator instances immutable and safe for concurrent trials.

Checks and evaluators return evidence and metrics. They never assert or decide the final
batch gate.
