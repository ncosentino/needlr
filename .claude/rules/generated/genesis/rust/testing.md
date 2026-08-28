---
# AUTO-GENERATED from .github/instructions/genesis/rust/testing.instructions.md — do not edit
paths:
  - "tests/**/*.rs"
  - "**/*_test.rs"
  - "**/test_*.rs"
  - "**/tests.rs"
---
# Rust CLI testing

Test the built binary end-to-end with `assert_cmd` and `predicates` in `tests/`.

```rust
use assert_cmd::Command;
use predicates::str::contains;

#[test]
fn prints_version() {
    let mut cmd = Command::cargo_bin("myapp").unwrap();
    cmd.arg("version")
        .assert()
        .success()
        .stdout(contains(env!("CARGO_PKG_VERSION")));
}
```

Rules:

- Build the command with `Command::cargo_bin("<binary-name>")`; pass arguments with
  `.arg(...)` / `.args(...)`.
- Assert on the outcome with `.assert().success()` / `.failure()`, and on output
  with `predicates` (`stdout` / `stderr` plus `contains`, `is_match`, …).
- Put reusable setup (a pre-configured `Command`, a temp directory) in a
  `tests/common.rs` module shared by the integration tests; use the `tempfile`
  crate for filesystem fixtures.
- Keep fast, logic-only checks as unit tests in a `#[cfg(test)] mod tests` block
  beside the code they exercise; reserve `tests/` for whole-binary behavior.
