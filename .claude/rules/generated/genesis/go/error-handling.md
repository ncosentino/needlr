---
# AUTO-GENERATED from .github/instructions/genesis/go/error-handling.instructions.md — do not edit
paths:
  - "cmd/**/*.go"
  - "internal/**/*.go"
---
# Go error handling

## Return errors with `RunE`

Always use `RunE` (not `Run`) so a command returns an error instead of calling
`os.Exit` from deep in the call tree:

```go
RunE: func(cmd *cobra.Command, args []string) error {
	if err := doWork(cmd.Context()); err != nil {
		return fmt.Errorf("doing work: %w", err)
	}
	return nil
},
```

## Wrap with `%w`, inspect with `errors.Is` / `errors.As`

Wrap errors as they propagate using `fmt.Errorf("context: %w", err)` so callers
can unwrap them. Match them with `errors.Is` (sentinel values) or `errors.As`
(typed errors) — never with a type assertion or a string comparison on the
message.

## Silence cobra's default error output

Set `SilenceUsage: true` and `SilenceErrors: true` on commands whose `RunE`
returns real (non-usage) errors. Without `SilenceUsage`, cobra prints the full
usage block on every failure, which is wrong for runtime errors. With
`SilenceErrors`, the single place that owns process exit prints the error once.

## Centralize exit-code handling

Let the top-level `Execute` translate a returned error into a status code and
print it once to the command's stderr writer. Do not scatter `os.Exit` or
`cobra.CheckErr` through subcommands — both bypass the testable exit path.
