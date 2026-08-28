# Pull request delivery

Every change ships through a feature branch and a pull request. Direct updates or
deletions of `main` are forbidden. Local checkpoint commits on a feature branch are
unrestricted.

Pull requests target `main` unless `.github/genesis-delivery.json` explicitly declares
stack support. A feature-branch base is not a harmless review convenience: merging into
an already-merged base can leave GitHub reporting a pull request as merged while its
commit never reaches `main`.

## Draft versus ready

`CI_DRAFT_MODE` controls how much validation a draft pull request runs. It is currently
set to `subset`, so a draft skips `build-and-test`, the Native AOT jobs, and package
validation.

- **"Open a draft PR"** means keep the pull request in draft while the configured
  draft-mode validation runs.
- **"Open a PR"** or **"publish a PR"** means make it ready for review, so a fresh full
  validation run publishes the stable `CI` check.

A draft that is green has not been fully validated. Read the check list rather than the
summary colour.

Guidance-only changes use the `guidance` validation scope. They retain the stable `CI`
check while skipping .NET build, packaging, and Native AOT jobs that cannot validate
Markdown, instruction metadata, or generated guidance mirrors.

## Title format

Use a conventional pull request title no longer than 72 characters. GitHub uses the title
as the squash commit subject and the body as the squash commit message, so both become
permanent history.

Allowed types are declared in `.github/genesis-delivery.json`:
`feat`, `fix`, `docs`, `refactor`, `test`, `style`, `build`, `ci`, `chore`, `perf`, `revert`.
Release preparation uses `chore: prepare vX.Y.Z release`.

## Review policy

`GENESIS_REVIEW_POLICY=copilot-one-approval` requires a ready pull request authored by the
Copilot bot to receive one trusted human approval on its **current head SHA**. Pushing
another commit invalidates that approval for delivery purposes.

Branch protection requires branches to be up to date, so each merge forces the next open
pull request to update. `CHANGELOG.md` is the usual conflict point.

## Fork workflow approval

Approving workflows from an external fork authorizes the entire proposed workflow,
including runner selection. Inspect workflow changes and confirm fork jobs remain on
GitHub-hosted runners before approving. See
[GitHub Actions runners](../github-actions.md).

## Disclosure before marking ready

Before marking a pull request ready, report omitted behavior, implementation gaps, test
results, technical debt, missing coverage, weak assertions, and assumptions. Fix every
high-severity gap and discuss every medium-severity gap before delivery.

State what was **not** done as plainly as what was. A reviewer who discovers an omission
after approving has been misled, even when nothing was stated falsely.
