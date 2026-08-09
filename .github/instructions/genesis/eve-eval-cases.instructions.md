---
applyTo: "**/evals/**/*.eval.ts"
---

# Eve Eval Case Rules

Eve discovers cases recursively under the application-root `evals/` directory from files
ending `.eval.ts`.

## Author the canonical case shape

- Default-export one `defineEval(...)` value or an array of `defineEval(...)` values for a
  dataset. The file path supplies the eval identity; do not author `id` or `name`.
- Drive the subject and assert inline inside one `async test(t)` function. Eve evals do not
  use the separate `PROMPT.md` plus hidden `EVAL.ts` convention.
- Keep shared constants, fixtures, scoring helpers, and judge adapters in sibling files
  whose names do not end in `.eval.ts`.

## Drive and grade through Eve

- Exercise the real Eve target through `t.send`, `t.start`, additional sessions, or the
  relevant HITL APIs. Assert on the resulting messages, structured output, events, tools,
  skills, and subagents.
- Prefer Eve's scoped assertions and `eve/evals/expect` builders for objective behavior.
  Use `t.require` only when later control flow depends on a precondition; otherwise record
  all applicable assertions so one run reports complete evidence.
- Apply custom metrics through `t.check` with a valid Eve `Assertion`. Validate custom
  assertion helpers independently, including score range, severity, and threshold behavior.
- Keep severity and thresholds on assertion handles. A threshold-free soft assertion is a
  tracked metric; `.atLeast(...)` is a soft threshold used by strict runs, and `.gate(...)`
  is an unconditional hard gate.

## Prefer the native judge lifecycle

- Use `t.judge.*` when Eve's built-in judge can express the criterion. This preserves Eve's
  judge resolution, assertion recording, reporting, and threshold semantics.
- A missing judge model, unavailable credentials, or a judge scoring error becomes a failed
  gate. If unavailable credentials or target capabilities should skip an eval, check them
  before driving the subject or recording assertions and call `t.skip(reason)` explicitly.
- Use a custom structured model call only when the built-in judge cannot represent the
  required scorecard. Resolve the judge separately, record its identity, bound retries,
  validate the complete response schema, and convert every dimension into explicit Eve
  assertions.
- Keep independent dimensions in separate `t.judge.*` assertions or tightly coherent custom
  judge groups. Aggregate outside the judge.
- A one-call multidimensional scorecard is a cost optimization, not independent evidence.
  Keep its scores threshold-free until a versioned human-labelled calibration set shows
  agreement with isolated graders and no material halo or rubric-order effects.
- Treat judge resolution, transport, malformed output, and schema failures as eval
  infrastructure errors. Preserve them as thrown eval errors or explicit failed gates;
  never translate them into a favorable score or a subject-quality score.
- Prefer a different pinned judge model or family from the subject. If the same family is
  intentionally used, document the limitation and validate the grader against
  human-labelled hard and borderline cases.
- Re-run judge calibration after changing the judge model, rubric, scorecard schema,
  evidence projection, or available tools.
