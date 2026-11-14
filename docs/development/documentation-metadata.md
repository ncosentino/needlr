---
description: Maintain documentation descriptions at their source and let the documentation build produce consistent page and social metadata.
---

# Documentation Metadata

Documentation descriptions are generated during the MkDocs build by
`docs/hooks/metadata.py`. This is deterministic processing of existing documentation,
not an AI request or a second store of manually maintained API descriptions.

## Source ownership

| Page | Maintained source | Generated description |
| --- | --- | --- |
| API type | C# XML documentation, rendered by DefaultDocumentation | Qualified API/version identity followed by the introductory summary |
| API index without prose | Package, namespace and version identity | A truthful reference identity, without invented behavior |
| Authored guide | Markdown introduction or explicit frontmatter | Page title and introduction, unless an explicit description is supplied |
| Homepage | Explicit frontmatter or the configured project description | The authored description or existing project description |

When changing a public API, maintain its XML contract. The API generator carries
that prose into Markdown, and MkDocs derives metadata from the introductory
paragraph. It does not select code, inheritance listings, tables or later member
remarks as substitutes for a missing type summary.

For an authored guide, an explicit description takes precedence:

```yaml
---
description: Register services and configure the application container.
---
```

Keep an explicit description consistent with the guide, or remove it to use the
generated introduction. Blank descriptions use the generated default; non-string
values fail the build. Code blocks, notes, images and tables are not introductory
prose.

Generated summaries are shortened at word boundaries to a target of 240 characters.
An indivisible identifier longer than that target is preserved rather than cut.
Explicit descriptions are not truncated or replaced, apart from whitespace
normalization.

## Rendering and checks

The same description supplies the ordinary HTML, Open Graph and Twitter metadata.
Escaping is applied only to the rendering context because MkDocs templates do not
autoescape by default. Source metadata remains plain text, and rendered page
content is returned unchanged.

The hosted Documentation metadata workflow exercises rendered metadata,
explicit-description precedence, API/version identity, introductory-prose
selection, safe escaping and unchanged titles/headings. These checks enforce
mechanical contracts; reviewers still need to verify that source prose accurately
describes the API or guide.

## Published versions

Development documentation uses this policy when regenerated. Releases apply it
when their API Markdown is built. Previously published stable and archived HTML
is intentionally preserved by the publication workflows, so updating a build hook
does not rewrite those frozen snapshots.

Backfilling descriptions in frozen snapshots is a separate reviewed metadata-only
maintenance operation. It must derive descriptions from that version's published
content and preserve everything outside the description metadata. Never edit the
final generated files by hand or use current API behavior to describe an older
version.
