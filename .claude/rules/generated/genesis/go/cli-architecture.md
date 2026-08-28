---
# AUTO-GENERATED from .github/instructions/genesis/go/cli-architecture.instructions.md — do not edit
paths:
  - "main.go"
  - "cmd/**/*.go"
---
# Go CLI architecture

Conventions for structuring a cobra-based command-line application.

## Keep `main` thin

`main.go` contains no business logic. It declares the build-metadata variables
(overridden via `-ldflags`), then calls into the `cmd` package:

```go
func main() {
	cmd.Execute(
		cmd.BuildInfo{Version: version, Commit: commit, Date: date},
		os.Exit,
		os.Args[1:],
	)
}
```

All command wiring lives in `cmd/`. Application logic that should not be imported
by other modules lives in `internal/`. Reserve `pkg/` for genuinely public,
importable APIs — most CLIs do not need it. Never use a `src/` directory.

## Build commands with constructors, not globals

Every command — including the root — is produced by a `new<Name>Cmd()` function
that returns a fresh `*cobra.Command`. Do NOT use package-level command globals
registered through `func init()`: globals make commands impossible to isolate in
tests, and `init()` registration hides the command tree and blocks dependency
injection.

```go
func newRootCmd(info BuildInfo) *cobra.Command {
	root := &cobra.Command{
		Use:           "myapp",
		Short:         "myapp does X.",
		SilenceUsage:  true,
		SilenceErrors: true,
	}
	root.AddCommand(newVersionCmd(info))
	return root
}
```

Register every subcommand explicitly in its parent's constructor via
`AddCommand`. This keeps the whole command tree visible in one place and lets a
command receive its dependencies as constructor arguments.

## Make process exit injectable

`Execute` takes an `exit func(int)` instead of calling `os.Exit` directly, so
tests can assert on the status code without terminating the test binary. Pass
`os.Exit` from `main`.

## One file per command

Put each subcommand in its own `cmd/<name>.go` with a matching
`cmd/<name>_test.go`. A subcommand file exposes exactly one `new<Name>Cmd`
constructor plus the helpers private to it.
