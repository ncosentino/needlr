---
applyTo: "src/**/*.rs"
---

# Rust CLI logging

For a command-line tool, use the `log` crate facade with a lightweight backend such
as `env_logger`. Reserve `tracing` for long-running services where spans and
structured subscribers earn their complexity.

```rust
fn main() -> ExitCode {
    env_logger::init(); // controlled by RUST_LOG, writes to stderr
    // ...
}

log::info!("starting up");
log::warn!("config file not found, using defaults");
```

Rules:

- Log diagnostics to **stderr**; keep stdout for the command's real output so it can
  be piped and parsed.
- Use the `log` macros (`error!` / `warn!` / `info!` / `debug!` / `trace!`) behind
  the facade so the binary — not its libraries — chooses the backend.
- Gate verbosity through a `-v` / `--verbose` flag or the `RUST_LOG` environment
  variable rather than hardcoding a level.
- Do not log an error and also return it; return it with context and let the top
  level report it once.
