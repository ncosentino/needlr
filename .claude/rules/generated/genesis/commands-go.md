---
# AUTO-GENERATED from .github/instructions/genesis/commands-go.instructions.md — do not edit
paths:
  - "**/*.go"
  - "**/go.mod"
---
# Go Build & Test Commands

## Resolve dependencies

```sh
go mod tidy
```

## Build

```sh
go build ./...
```

## Test

```sh
go test ./...
```

## Lint

```sh
golangci-lint run
```
