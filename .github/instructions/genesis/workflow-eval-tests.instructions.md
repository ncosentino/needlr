---
applyTo: "**/*.Tests.Eval/**/*EvalTests.cs"
---

# Agentic Workflow Eval Test Rules

Each eval test represents one workflow scenario and delegates execution to shared
harness or runner code.

- Place executable C# workflow evals in a project or directory ending `.Tests.Eval` so
  the complete eval guidance applies without matching ordinary business evaluation code.
- Exercise the same production workflow, pipeline, loop, middleware, tools, and
  diagnostics path used outside tests. Do not recreate workflow behavior in the eval.
- Use committed or code-generated pinned seeds. Never discover a dynamic latest run.
- Use the established runner's development-versus-held-out classification when one exists.
  Do not invent a storage convention, and do not expose held-out status to the subject.
- Structure scenarios according to the test runner's parallelism model. With xUnit, use
  one scenario per class because methods within one class serialize.
- Create fresh workflow state, workspace, and stage graph inside each trial.
- Return checks and evaluator results from the run; do not assert inside trial callbacks.
- A check must prove the workflow or stage did its job. It is worthless if it passes when
  the subject does nothing and only the seed was already valid.
- Add paired or metamorphic scenarios for invariance and contrastive behavior. For
  multi-turn workflows, include relevant long-range context, distractors, and topic shifts.
- For comparisons, use the same fixture, seed, checks, evaluators, subject provider, and
  judge while changing one variable.
