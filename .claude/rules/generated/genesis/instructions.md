---
# AUTO-GENERATED from .github/instructions/genesis/instructions.instructions.md — do not edit
paths:
  - ".github/instructions/**/*.md"
  - ".github/instructions/*.md"
---
# Instruction File Authoring Rules

These rules apply when writing or editing Copilot instruction files in this repository.

## Never reference other instruction files by name

Do NOT link to, mention, or cross-reference other `.instructions.md` files by name. Glob matching in the `applyTo` frontmatter automatically loads all relevant instruction files for any given file — explicit cross-references are redundant and become stale when files are renamed or reorganized.

```
// ❌ WRONG
General test and mock-initialization rules apply alongside these harness-specific
rules.
For full examples, see `tests-repository-and-service.instructions.md`.

// ✅ ACCEPTABLE
More specific instructions for this scenario take precedence over these general rules.
These are general rules — type-specific instructions may refine them.
```

It IS acceptable to describe the relationship between levels of guidance without naming specific files.

## Share mandatory guidance through overlapping globs

Path-specific instruction files have no portable import or `references/` mechanism.
Plain Markdown links are not guaranteed to load their target in every Copilot or Claude
client.

When multiple file populations need the same mandatory rule, put it in one concise
instruction file whose `applyTo` is the union of those populations. Matching common and
specialized instructions compose automatically. Use links only for optional background
that is not required for compliance.

## Keep `genesis/` files read-only

`.github/instructions/genesis/` is replaced wholesale on every sync (delete + recopy,
no merge) — an in-place edit is silently lost with no warning. Treat every file here
as read-only, even ones you disagree with; never edit the wording, even to "fix" it.

## Add your own instructions instead of editing genesis/ ones

New or overriding guidance goes in a file outside `genesis/`, never inside it — even
for a glob that overlaps an existing `genesis/` file (matching files all load
together). State explicitly when a file overrides another, and why. A folder beside
`genesis/` (handy if you ever pull instructions from elsewhere too) keeps things
organized, but isn't required — any location outside `genesis/` works.

## Size the glob to the rule's real scope

Match the glob to the rule's real population: an exact filename for one config file,
a suffix union for a family of similar roles (`**/*Service.cs,**/*Repository.cs,...`),
a blanket extension match only when genuinely true for every file of that kind. Too
broad injects irrelevant guidance; too narrow misses files the rule should cover and
creates false confidence it's handled.

## Keep guidance durable

Write durable contracts and failure-prevention rules, not snapshots of fast-moving
technology. Do not hardcode model IDs, pricing, provider quotas, preview API shapes,
or similar volatile facts in general instructions. Keep exact values in the
configuration or manifests that own them, and verify current vendor documentation
when implementation depends on a version-sensitive capability.

## Be concise

Write the minimum content that gives correct direction — every extra line loads into
context on every matching file. Add DO/DON'T examples only when a rule is genuinely
easy to get wrong in a specific way; skip them when one clear sentence covers it.
