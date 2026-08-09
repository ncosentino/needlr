---
name: review-changes
description: >
  Review the current project diff before commit, push, or pull-request delivery
  against applicable instructions, project docs and ADRs, repository-declared
  validation, and existing CI evidence. Use for review requests, code review,
  PR review, or delivery-readiness checks.
---

# Review project changes

This skill owns review procedure, not project standards. Current instructions, docs,
ADRs, manifests, tests, and workflows remain authoritative.

## Review boundary

- Judge changed lines and their direct invariant blast radius.
- Report pre-existing divergence separately and never include it in the verdict.
- Do not demand unrelated migration work because docs describe a target state.
- Do not invent findings for a clean diff.
- Review is read-only unless the user explicitly asks for fixes.

## 1. Resolve the scope

Confirm the worktree and branch:

```powershell
git rev-parse --show-toplevel
git branch --show-current
git status --short
```

Use scope in this order:

1. Explicit refs, pull request, or paths supplied by the user.
2. All uncommitted changes: unstaged, staged, and untracked.
3. Otherwise `git merge-base origin/main HEAD` through `HEAD`.

Use `git --no-pager diff`, `git --no-pager diff --cached`, and full reads for
untracked files. For a pull request, confirm the actual base/head with `gh pr view`
and `gh pr diff`.

State the selected scope and changed files.

## 2. Resolve governing sources

Resolve applicable instructions:

```powershell
pwsh scripts/guidance/Get-ApplicableInstructions.ps1 -Path <changed-paths>
```

Read every returned instruction in full.

If `.github/genesis-guidance.json` exists, read its declared docs map and review
metadata. Otherwise use the README and discover existing docs/ADR indexes without
assuming they exist. Follow relevant links from changed docs and matching
instructions.

Project-owned instructions and accepted project ADRs may specialize Genesis-managed
defaults. Do not edit `.github/instructions/genesis/` to express a local override.

## 3. Resolve validation

Inventory declared validation/build surfaces:

```powershell
pwsh scripts/guidance/Get-ValidationInventory.ps1
```

Read the returned package scripts, solution/project files, language manifests,
workflows, and Genesis delivery metadata. Inspect their actual definitions before
choosing commands.

- Run only the smallest offline command that covers the changed behavior.
- Use package/workspace filters, project/test selectors, or equivalent repository
  scoping when declared by the toolchain.
- Do not invent a command that the repository does not declare or document.
- Do not run complete suites, browser/platform matrices, hosted scenarios, or
  credentialed/live checks on a workstation.
- Pull request CI and declared runner profiles own complete and hosted evidence.
- For a pull request, inspect `gh pr checks` instead of reproducing heavy work.

Record every command/result and every required check that was not run.

## 4. Review what gates do not prove

Read each changed file and inspect:

- correctness, failure handling, and deterministic behavior;
- architecture and trust-boundary compatibility;
- manifest/schema and generated-output consequences;
- dependency drift and unsupported version changes;
- tests or gates missing for introduced behavior;
- docs/instruction authority and current-truth discipline;
- credentials, untrusted inputs, destructive actions, and privacy.
- for public repositories, public artifacts disclose no local filesystem names/paths
  or private-repository information in tracked files, commits, comments, or PR text.

Use the current governing source for the exact rule. These categories are not a
second standards checklist.

## 5. Reflect on guidance

Treat review as a bounded feedback loop, not a default instruction-edit trigger.

Recommend a guidance change only when the review shows either:

- one significant misstep with material risk or impact; or
- repeated evidence of the same avoidable misstep.

The lesson must be generalizable, supported by concrete evidence, and assigned to the
correct owner. Do not propose guidance for a one-off, speculative, hyper-specific, or
stylistic incident, or when current guidance or executable checks already cover it
clearly and effectively. Prefer code or tests for enforceable behavior, instructions
for recurring exact rules, docs for rationale, skills for procedures, and `AGENTS.md`
only for safeguards needed before any file is selected.

Review remains read-only. Report no guidance change when the threshold is not met; do
not edit guidance automatically.

## 6. Report

Open with:

- `Scope:` reviewed range or paths;
- `Verdict:` `Approve`, `Approve with nits`, or `Request changes`;
- `Validation:` observed/passed/failed/not-run evidence.
- `Guidance reflection:` `no change warranted` or one concrete candidate with its
  evidence and intended owner.

Group introduced findings by severity:

- **Blocker** - broken behavior, security/destructive risk, failing required
  validation, or violation of an accepted architecture/delivery boundary.
- **Major** - clear correctness or contract defect that should be fixed before merge.
- **Minor** - bounded maintainability, coverage, or guidance defect.
- **Nit** - optional polish only.

Every finding includes:

`severity - file:line - issue - governing source - concrete fix`

If there are no introduced findings, say so plainly. State uncertainty and missing
evidence instead of implying an unrun check passed.
