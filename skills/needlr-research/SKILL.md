---
name: needlr-research
description: Resolve authoritative Needlr evidence from a source checkout, a consumer package version, or current web research for greenfield planning.
---

# Needlr Research

Use this workflow before answering Needlr questions or changing Needlr-related
code. The goal is not always to use the newest source; it is to use the source
that matches the caller's actual context.

## 1. Resolve the context

### Needlr source checkout

Treat the workspace as a Needlr source checkout when it contains the Needlr
solution and repository guidance, or its repository identity is
`ncosentino/needlr`.

Use the checkout as the primary authority:

1. Read `AGENTS.md` and applicable `.github/instructions/`.
2. Read related accepted records under `docs/adr/`.
3. Trace implementation and executable tests.
4. Use docs and examples to explain the verified behavior.

Do not replace fresher local evidence with indexed web results.

### Consumer repository with Needlr references

Inspect the consumer before recommending APIs:

1. Find `NexusLabs.Needlr*` package references in project files, central package
   management, lock files, or restored assets.
2. Resolve variables and determine the effective package version when possible.
3. Match that package to its Needlr release, tag, changelog, documentation, and
   source.
4. Clearly separate APIs available to that version from newer unreleased or
   subsequently released behavior.

NuGet prerelease normalization may differ from git tag spelling. Verify the
release mapping rather than assuming the strings are identical.

### Greenfield planning without a reference

When no Needlr checkout or package reference exists because adoption is still
being planned, perform current web research:

1. Search the official documentation at
   `https://www.devleader.ca/projects/needlr/`.
2. Search `github.com/ncosentino/needlr` for current source, tests, examples,
   releases, and changelog entries.
3. Check NuGet metadata for the latest published package line.
4. State whether guidance targets the latest release or unreleased `main`.

Planning without an existing dependency is the explicit case where current web
search is required.

## 2. Apply the evidence order

Prefer evidence in this order within the resolved context:

1. Explicit architectural decisions and repository instructions.
2. Public contracts and implementation.
3. Executable tests that demonstrate observable behavior.
4. Version-matched documentation and examples.
5. Release notes and package metadata.
6. External framework documentation for Roslyn, .NET, ASP.NET Core, Carter, or
   another dependency.

Use web search when evidence is not local, when matching a consumer release, or
when verifying an external dependency. Never guess an API, diagnostic, package,
or lifecycle rule.

## 3. Report with version clarity

- State whether the answer targets a local checkout, a specific package
  version, the latest published release, or current `main`.
- Cite the files, tests, releases, or official URLs that support important
  claims.
- Distinguish verified facts from assumptions and recommendations.
- If the available evidence does not establish an answer, say what remains
  unknown.
