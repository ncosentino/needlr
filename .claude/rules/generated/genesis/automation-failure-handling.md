---
# AUTO-GENERATED from .github/instructions/genesis/automation-failure-handling.instructions.md — do not edit
paths:
  - "**/*.ps1"
  - "**/*.sh"
  - ".github/workflows/**/*.yml"
  - ".github/workflows/**/*.yaml"
---
# Automation Must Fail Loudly

A script or workflow that hides a failure is worse than one that has no error handling at all: it
publishes half-built artifacts, reports green, and moves the discovery of the problem to whoever
consumes the output.

The rule is not "never suppress a failure." It is **never suppress a failure you did not decide to
suppress.** Deliberate, documented tolerance is fine. Incidental tolerance is a defect.

## Do not swallow failures

**Shell**

- `command || true` and `command || :` on a step whose success matters
- `set +e` left in effect for the rest of the script
- `2>/dev/null` on a call whose failure should stop the run — this hides the reason, not just
  the noise
- `exit 0` in a trap or at the end of a script that ran a failing command
- Omitting `set -euo pipefail`, which leaves a failing command mid-pipeline invisible

**PowerShell**

- `$ErrorActionPreference = 'Continue'` or `'SilentlyContinue'` at script scope
- `-ErrorAction SilentlyContinue` on a call whose result is then used
- `catch { }` with an empty body, or a catch that logs and continues as if nothing happened
- Ignoring `$LASTEXITCODE` after invoking a native executable — PowerShell does not raise on a
  nonzero native exit code

**Workflows**

- `continue-on-error: true` on a step that gates correctness
- `if: always()` on a summary step that then exits zero regardless of what preceded it
- A final step that unconditionally succeeds and masks an earlier failure

## Do not fabricate success

A fallback that produces a success-shaped result when the real operation failed is the same defect
wearing a nicer outfit: an empty array where a fetch failed, a default object where a parse failed,
a "0 items processed" summary where the query never ran. Downstream steps cannot tell these apart
from genuine results.

Handle a failure only where the code can do something meaningful about it. Otherwise let it
propagate.

## Deliberate non-blocking steps

Some steps genuinely should not fail a run — best-effort cache warming, advisory linting, optional
telemetry, a cleanup that races with something else. These are legitimate. Two requirements:

1. The tolerance is explicit at the point where it happens.
2. A comment on that line says *why*, so a reviewer can tell intent from accident.

```yaml
# Advisory only: link checking depends on third-party availability
# and must not gate the release.
- name: Check external links
  continue-on-error: true
  run: ./scripts/check-links.sh
```

Without the comment, the next reader has to guess whether the suppression was reasoned or
copy-pasted — and guessing wrong in either direction is expensive.
