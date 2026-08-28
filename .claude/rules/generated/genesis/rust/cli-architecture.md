---
# AUTO-GENERATED from .github/instructions/genesis/rust/cli-architecture.instructions.md — do not edit
paths:
  - "src/main.rs"
  - "src/cli.rs"
---
# Rust CLI architecture

Conventions for a clap-based command-line application.

## Keep `main` thin; return an `ExitCode`

`main` delegates to a `run` function and converts the result into a process exit
code. Returning `ExitCode` (rather than `-> anyhow::Result<()>`) keeps control over
the exit status and prints errors cleanly:

```rust
fn main() -> ExitCode {
    match run() {
        Ok(()) => ExitCode::SUCCESS,
        Err(err) => {
            eprintln!("Error: {err:#}");
            ExitCode::FAILURE
        }
    }
}

fn run() -> anyhow::Result<()> { /* parse + dispatch */ }
```

Avoid `fn main() -> anyhow::Result<()>`: it prints the error via `Debug` (noisy)
and gives up exit-code control.

## Define the CLI with clap derive

Put the `#[derive(Parser)]` root and `#[derive(Subcommand)]` enum in `src/cli.rs`,
using clap v4 with the `derive` feature. Doc comments become help text.

```rust
#[derive(Parser)]
#[command(name = "myapp", version)]
pub struct Cli {
    #[command(subcommand)]
    pub command: Command,
}

#[derive(Subcommand)]
pub enum Command {
    /// Print version information
    Version,
}
```

## Module layout

A single binary crate is the norm for a CLI: `src/main.rs` declares modules and
holds `main`/`run`; `src/cli.rs` holds the parser; one module per subcommand's
logic. Promote shared logic into a library crate (`src/lib.rs`) only when another
crate needs to import it. Do not use a `src/` sub-layout copied from other
ecosystems — follow Cargo's conventions.

## Dispatch in one place

`run` matches on the parsed `Command` and calls one handler per variant. Handlers
return `anyhow::Result<()>` and propagate errors with `?`.
