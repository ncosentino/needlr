---
# AUTO-GENERATED from .github/instructions/genesis/go/testing.instructions.md — do not edit
paths:
  - "**/*_test.go"
---
# Go command testing

Test commands by constructing them, injecting arguments, and capturing output —
never by shelling out or inspecting the real `os.Stdout`.

```go
func TestVersionCommand(t *testing.T) {
	var out bytes.Buffer
	root := newRootCmd(BuildInfo{Version: "1.2.3"})
	root.SetOut(&out)
	root.SetArgs([]string{"version"})

	if err := root.Execute(); err != nil {
		t.Fatalf("version command returned error: %v", err)
	}
	if !strings.Contains(out.String(), "1.2.3") {
		t.Fatalf("output %q missing version", out.String())
	}
}
```

Rules:

- Build the command from its `new<Name>Cmd` constructor — never reference a
  package-level global.
- Inject arguments with `cmd.SetArgs`; capture stdout/stderr with `cmd.SetOut` /
  `cmd.SetErr` and a `bytes.Buffer`.
- To assert on the exit status, pass a recording `exit func(int)` into the code
  under test instead of letting it call `os.Exit`.
- Use `t.TempDir()` for filesystem fixtures and `t.Setenv` for environment
  variables so tests clean up after themselves and remain isolated.
- Prefer table-driven tests for commands with several input variations.

The standard library `testing` package is sufficient for command tests; add an
assertion library only when assertions become genuinely repetitive.
