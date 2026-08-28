---
# AUTO-GENERATED from .github/instructions/genesis/rust/tooling.instructions.md — do not edit
paths:
  - "Cargo.toml"
---
# Rust tooling, Cargo, and CI

## Everyday commands

```sh
cargo build
cargo test
cargo clippy --all-targets -- -D warnings
cargo fmt --all
```

Treat clippy warnings as errors in CI (`-D warnings`) and fix them rather than
`#[allow(...)]`-ing them; when an allow is genuinely needed, scope it narrowly and
explain why.

## Formatting

Format with `rustfmt` (`cargo fmt`) and check it in CI with
`cargo fmt --all -- --check`. Keep to the default style unless the whole project
agrees on a `rustfmt.toml`.

## Dependencies and `Cargo.lock`

Keep dependencies minimal and let `cargo` resolve transitive versions. **Commit
`Cargo.lock`** — this is a binary crate, so the lockfile pins reproducible builds.
`cargo build` does not fail on a declared-but-unused dependency, so remove
dependencies you no longer use.

## Optional functionality

Gate optional behavior and its dependencies behind Cargo `[features]` with
`optional = true` dependencies and `#[cfg(feature = "...")]`, rather than compiling
everything unconditionally.

## Configuration

For environment-variable binding, prefer clap's `env` feature
(`#[arg(env = "MYAPP_THING")]`). For a config file, use `serde` plus a format crate
such as `toml`; the `config` crate is a heavier all-in-one option when you want
file + env + defaults merged for you.

## CI

Install the toolchain with `dtolnay/rust-toolchain@stable` and cache builds with
`Swatinem/rust-cache`. Run build, test, `clippy -- -D warnings`, and `fmt --check`
across the operating systems you support.
