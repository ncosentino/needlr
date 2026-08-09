---
applyTo: "**/*.Tests.Eval/**/*Runner.cs,**/*.Tests.Eval/**/*Harness.cs,**/*.Tests.Eval/**/*Fixture*.cs,**/*.Tests.Eval/**/*Experiment*.cs,**/*.Tests.Eval/**/*ResultCollector.cs,**/*.Tests.Eval/**/*QualityGate.cs,**/*.Tests.Eval/**/*Policy.cs"
---

# Agentic Workflow Eval Runner Rules

Runners and harnesses turn production workflow executions into complete, repeatable
evaluation evidence.

## Enforce the suite success contract

- Consume the repository's established success criteria: dimensions, metrics, targets,
  denominators, aggregation, and hard-gate versus diagnostic policy. Do not invent a new
  configuration format.
- Protect held-out cases from prompt, rubric, and policy tuning. Development and final
  validation results must remain distinguishable in reports.
- Aggregate per dimension and per slice before applying policy. Do not let a global average
  hide a failed safety, difficulty, or population slice.
- Compare candidates and baselines on identical cases and report task quality alongside
  latency, token usage, and cost when those dimensions are relevant.
- Run judge-calibration cases separately from subject capability trials. Report per-dimension
  false-pass, false-fail, agreement, ranking, and grader-infrastructure metrics before
  allowing a semantic score to affect policy.
- Preserve isolated-grader and bundled-scorecard results as distinct evidence so cost
  optimizations cannot silently replace the calibrated measurement.

## Invoke production behavior

- Run the production workflow or pipeline through its real public seam.
- Keep conversion from a completed run to evaluator inputs separate from scoring and
  from gate policy.
- Preserve native structured messages, responses, tool calls, stage results, and
  diagnostics. Flatten to text only inside a grader that explicitly needs text.

## Isolate and repeat

- Create one fresh item scope, workspace, and output location per trial.
- Keep shared fixtures, judge hosts, and concurrency limiters thread-safe; keep mutable
  scenario state local to the trial.
- Bound concurrency across the whole eval process, not independently in every scenario.
- Release concurrency capacity before retry delays.
- Treat retries as additional attempts within the same trial, not as extra successful
  samples.

## Capture complete evidence

- Capture diagnostics for every workflow stage, including stages that throw.
- Record termination reason, tool sequence and failures, token usage, durations, warnings,
  outputs, and artifacts.
- Persist immutable, schema-versioned result snapshots instead of live SDK evaluator
  objects.
- Write each trial's artifacts before applying one final batch gate.
- Do not let one crash abort or erase the remaining trials; record it as a failed or
  infrastructure outcome and finish the batch.
- Make insufficient statistical evidence Inconclusive rather than forcing pass or fail.
- Classify malformed grader results as grader or infrastructure failures and preserve them
  separately from subject-quality failures.
