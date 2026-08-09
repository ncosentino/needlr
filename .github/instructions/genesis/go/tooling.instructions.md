---
applyTo: "go.mod, .golangci.yml"
---

# Go tooling, modules, and CI

## Modules

Keep `go.mod` minimal and let `go mod tidy` manage the dependency graph. After
adding or removing an import, run `go mod tidy` and commit the resulting `go.mod`
and `go.sum` together. `go build` neither adds missing requirements nor removes
unused ones — `go mod tidy` does both.

## Everyday commands

```sh
go build ./...
go vet ./...
go test -race ./...
golangci-lint run
```

Run `go test` with `-race` in CI to catch data races.

## Linting

Lint with golangci-lint using the v2 configuration schema (`version: "2"`) and
format with `gofumpt`. Fix findings rather than suppressing them; when a
`//nolint` directive is genuinely necessary it must name the linter and explain
why.

## Version injection

Inject build metadata at link time instead of hardcoding it, and expose it
through a `version` subcommand:

```sh
go build -ldflags "-s -w \
  -X main.version=$(git describe --tags) \
  -X main.commit=$(git rev-parse --short HEAD) \
  -X main.date=$(date -u +%Y-%m-%d)" .
```

`-s -w` strips debug symbols; add `-trimpath` to remove local filesystem paths
from the binary.

## CI

Pin the Go version with `go-version-file: go.mod` rather than hardcoding it in
the workflow. Verify module hygiene by running `go mod tidy` and failing on any
diff:

```sh
go mod tidy
git diff --exit-code go.mod go.sum
```
