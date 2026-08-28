---
# AUTO-GENERATED from .github/instructions/genesis/raw-string-literals.instructions.md — do not edit
paths:
  - "**/*.cs"
---
# Raw String Literal Rules

## Align the opening `"""` with the closing `"""`

Multi-line raw string literals (C# 11+) strip leading whitespace based on the column of the **closing** `"""`. Put the opening `"""` on its own line at the **same indent** as the closing `"""`, and indent the content to that same column.

```csharp
// ❌ WRONG — opening """ dangles at end of line, offset from closing
var source = """
    using System;
    """;

// ✅ CORRECT — opening, content, closing all at the same column
var source =
    """
    using System;
    """;
```

Both compile, but the wrong form is harder to scan and obscures the literal's boundaries. Single-line raw strings (`var s = """value""";`) are exempt — there is no closing `"""` on a separate line to align with.
