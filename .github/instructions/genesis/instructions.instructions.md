---
applyTo: ".github/instructions/**/*.md,.github/instructions/*.md"
---

# Instruction File Authoring Rules

These rules apply when writing or editing Copilot instruction files in this repository.

## Never reference other instruction files by name

Do NOT link to, mention, or cross-reference other `.instructions.md` files by name. Glob matching in the `applyTo` frontmatter automatically loads all relevant instruction files for any given file — explicit cross-references are redundant and become stale when files are renamed or reorganized.

```
// ❌ WRONG
See also: `tests-common.instructions.md` for general rules.
The general mock init principles are defined in `tests-common.instructions.md`.
For full examples, see `tests-repository-and-service.instructions.md`.

// ✅ ACCEPTABLE
More specific instructions for this scenario take precedence over these general rules.
These are general rules — type-specific instructions may refine them.
```

It IS acceptable to describe the relationship between levels of guidance without naming specific files.
