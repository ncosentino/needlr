---
applyTo: "src/**/*.rs"
---

# Rust error handling

## `anyhow` for the binary, `thiserror` for libraries

In application (binary) code, use `anyhow::Result<T>` and propagate errors with
`?`. If you extract a reusable library crate, give it its own error enum with
`thiserror` so callers can match on specific variants.

## Add context as errors propagate

Use `anyhow::Context` to attach a human-readable message at each layer:

```rust
let data = std::fs::read_to_string(&path)
    .with_context(|| format!("reading {}", path.display()))?;
```

Prefer `with_context(|| ...)` (the message is built lazily, only on error) over
`context(format!(...))` when constructing the message is non-trivial.

## Surface errors once, at the top

Let `run` return `anyhow::Result<()>` and let `main` print the error and choose the
exit code. Do not `eprintln!` an error and then also return it — that reports it
twice. Format the full cause chain with the alternate flag:
`eprintln!("Error: {err:#}")`.

## Exit codes

`0` is success, non-zero is failure. When scripts need to distinguish failure
classes, map them to distinct codes via `ExitCode`. A broken pipe is conventionally
treated as success.
