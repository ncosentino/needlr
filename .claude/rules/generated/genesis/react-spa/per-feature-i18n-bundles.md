---
# AUTO-GENERATED from .github/instructions/genesis/react-spa/per-feature-i18n-bundles.instructions.md — do not edit
paths:
  - "**/src/features/**/i18n/*.json"
---
# Per-Feature i18n Bundle Rules

These rules apply to translation bundles that live at the
`src/features/<feature-id>/i18n/<locale>.json` path shape. Bundles at
other paths (e.g. a monolithic `src/i18n/`) are outside the scope of
these rules.

## Namespace = parent feature folder name

The i18n namespace name is exactly the parent feature folder name. A
bundle at `src/features/daemons/i18n/en.json` belongs to the namespace
`daemons`. Components inside the feature access it via
`useTranslation('daemons')`.

No feature reads keys from a sibling feature's namespace. If two
features need the same string, lift it into the shared core
namespace — don't reach across.

## One file per locale

Each bundle holds exactly one locale: `en.json`, `es.json`, `de.json`,
`fr-CA.json`. Never combine multiple locales in a single file.

## Plural-aware keys use i18next suffixes

When a value's text depends on a count, use i18next's canonical plural
suffixes. The resolver picks the right key from the count + locale.

```json
{
  "workersRunning_zero": "no workers running",
  "workersRunning_one": "{{count}} worker running",
  "workersRunning_other": "{{count}} workers running"
}
```

Supported suffixes: `_zero`, `_one`, `_two`, `_few`, `_many`, `_other`.
Don't invent your own plural scheme (`workersRunningOne`,
`workersRunningMany`, etc.) — the i18next resolver won't fall back to
them correctly across locales with different plural rules.

## Bundle shape

Use nested objects to group related keys. Reflect UI structure when it
helps readability, not when it adds depth for its own sake.

```json
{
  "title": "Daemons",
  "list": {
    "title": "Daemons",
    "addCta": "Add daemon",
    "empty": {
      "title": "No daemons configured",
      "description": "Add your first daemon to get started.",
      "cta": "Add daemon"
    }
  },
  "row": {
    "actions": {
      "edit": "Edit",
      "test": "Test",
      "remove": "Remove"
    },
    "workersRunning_zero": "no workers running",
    "workersRunning_one": "{{count}} worker running",
    "workersRunning_other": "{{count}} workers running"
  }
}
```

Access: `t('row.actions.edit')`, `t('row.workersRunning', { count })`.
