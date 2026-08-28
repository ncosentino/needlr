---
# AUTO-GENERATED from .github/instructions/genesis/go/logging.instructions.md — do not edit
paths:
  - "cmd/**/*.go"
  - "internal/**/*.go"
---
# Go logging

Use the standard library `log/slog` for structured logging. It needs no external
dependency and is the current ecosystem default for command-line tools.

```go
logger := slog.New(slog.NewTextHandler(os.Stderr, &slog.HandlerOptions{
	Level: slog.LevelInfo,
}))
slog.SetDefault(logger)

slog.Info("starting", "addr", addr, "version", version)
```

Rules:

- Log to **stderr**, not stdout — stdout is reserved for the command's actual
  output so it can be piped and parsed.
- Use structured key/value attributes (`slog.Info("msg", "key", value)`); do not
  format variable data into the message string.
- Use lowercase, constant message strings and `camelCase` attribute keys.
- Do not log and return the same error — wrap and return it, and let the single
  top-level handler decide whether to print it.
- Use `fmt.Fprintln(cmd.OutOrStdout(), ...)` for user-facing command output;
  reserve `slog` for diagnostics.
