---
# AUTO-GENERATED from .github/instructions/genesis/evals-common.instructions.md — do not edit
paths:
  - "**/evals/**/*"
  - "**/*.Tests.Eval/**/*.cs"
---
# Evaluations

- Define each decision-relevant dimension with metric/unit, population/denominator,
  aggregation, target/baseline, and gate-versus-diagnostic policy.
- One case isolates one objective and capability/risk dimension. Preserve important
  production/risk slices rather than relying on one aggregate.
- Keep development/regression cases separate from held-out validation. An inspected
  held-out failure moves into development/regression coverage.
- Preserve case and label provenance. Model-generated cases/labels require
  deduplication and human review.
- A case is one logical contract, a trial one independent execution, and an attempt an
  infrastructure retry. Retries never hide subject failures.
- Grade final outcome/environment evidence before narrative quality.
- Use deterministic checks for objective contracts; model judges only for semantic
  dimensions.
- Model judges return evidence plus an explicit inconclusive result, use strict output
  schemas, and treat malformed output as eval infrastructure failure.
- Calibrate judges against versioned human-labelled clear, borderline, adversarial,
  missing-evidence, and malformed-output cases before scores gate delivery.
- Keep subject and judge resolution independent; disclose/correct correlated-evaluator
  risk.
- Record every trial, usage, duration, artifacts, failures, environment, rubric/model
  versions, and source revision before applying gate policy.
- Run costly/nondeterministic judge suites through an explicit eval workflow, not the
  ordinary unit-test gate.
