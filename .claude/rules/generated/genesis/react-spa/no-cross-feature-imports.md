---
# AUTO-GENERATED from .github/instructions/genesis/react-spa/no-cross-feature-imports.instructions.md — do not edit
paths:
  - "**/src/features/**/*.{ts,tsx}"
---
# No Cross-Feature Imports

This rule applies to any TypeScript/TSX file inside a feature folder
under `src/features/<feature-id>/`. It enforces feature isolation —
the architectural invariant that makes features independently
deletable, testable, and reorganizable.

## The rule

A file under `src/features/<A>/` MUST NOT import from
`src/features/<B>/` (any sibling feature). Imports from
`src/features/<A>/` (the feature's own files), from `src/core/`, and
from third-party packages are all fine.

```ts
// File: src/features/daemons/components/DaemonsPage.tsx

// ❌ WRONG — reaches into a sibling feature
import { DecisionsBadge } from '@/features/decisions/components/DecisionsBadge';
import { issueQueryKeys } from '../../issue-stream/services/queryKeys';

// ✅ CORRECT — own feature + core
import { useDaemons } from '../hooks/useDaemons';
import { Button } from '@/core/ui/button';
import { eventBus } from '@/core/state/eventBus';
```

## How features communicate across boundaries

When feature A needs something feature B owns, use one of three
sanctioned mechanisms:

1. **URL navigation** to a route owned by the other feature.
   `navigate('/decisions/123')` — feature A doesn't import any
   decisions code; it just hands control to a decisions-owned route.

2. **Shared data living in core**, queried via react-query (or your
   chosen data layer). Both features import the same query key
   factory and hook from `@/core/api/`; neither knows the other
   exists.

3. **Typed event bus** in core (if one exists). Feature A emits on a
   channel registered in the bus's `AppEventChannels` type; feature B
   subscribes. The channel name is the contract; neither side imports
   the other.

If none of those three fit, the right move is usually to lift the
shared concern into `src/core/` — not to break the import boundary.

## Why this rule

Cross-feature imports create implicit coupling that compounds. Two
features that look isolated suddenly can't be touched independently
because each transitively depends on the other through a chain of
"just one little import." Hard isolation makes future refactors
(splitting a feature into two, deleting a feature, moving a feature
into its own package) mechanical rather than archaeological.
