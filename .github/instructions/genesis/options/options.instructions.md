---
applyTo: "**/*Options.cs"
---

# Options types

- Use a `public sealed class` with Needlr `[Options]`; do not register it manually in
  a plugin.
- The default section strips the `Options` suffix. Pass an explicit `:`-delimited
  section when configuration uses another path.
- Use `ValidateOnStart = true` and data annotations for required/valid startup
  configuration.
- Options contain properties and validation only, never business logic.
- Inject `IOptions<T>` by default, `IOptionsSnapshot<T>` for scoped reload, and
  `IOptionsMonitor<T>` for singleton change observation.
- Multiple instances use named `[Options(..., Name = "...")]` declarations and are
  resolved through snapshot/monitor `Get(name)`.
