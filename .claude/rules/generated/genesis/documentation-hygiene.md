---
# AUTO-GENERATED from .github/instructions/genesis/documentation-hygiene.instructions.md — do not edit
paths:
  - "**/*.md"
---
# Documentation Example Hygiene

Examples get copied. Treat every command, config snippet, and URL in committed Markdown as
something a reader will paste into a real environment without editing it first.

## Never commit real values

Keep these out of examples entirely:

- Secrets and credentials — API keys, access tokens, passwords, connection strings, private keys
- Production identifiers — account, tenant, subscription, organization, and project IDs; storage
  bucket and container names; internal hostnames; real customer or user IDs
- Live endpoints — internal URLs, and any host that resolves to real infrastructure

A value that is expired, rotated, or "only" from a sandbox still belongs out of the repository.
Readers cannot tell which is which, and git history keeps whatever was committed.

## Use placeholders that cannot be mistaken for real values

| Kind | Use |
|------|-----|
| Domains | `example.com`, `example.org` (reserved by RFC 2606) |
| Identifiers | `<account-id>`, `<tenant-id>` — angle brackets, kebab-case |
| GUIDs | `00000000-0000-0000-0000-000000000000` |
| Secrets | `<your-api-key>` |
| Email | `user@example.com` |

Angle brackets matter: they fail loudly if pasted unedited, whereas a realistic-looking fake gets
used as-is.

Showing the *shape* of a credential is fine when the format is the point — `sk_test_` followed by
`<redacted>` teaches the prefix without leaking a key. What matters is that no example contains a
value that could authenticate to anything.

## Keep paths portable

Absolute paths are fine when the path is genuinely fixed — an install location or a platform
convention. What does not belong is a path rooted in one developer's machine:

```
# Avoid — nobody else has this path
C:\Users\jsmith\source\repos\MyApp\config.json
/home/jsmith/projects/myapp/config.json

# Prefer — relative to the repository or an environment variable
./config.json
$HOME/.config/myapp/config.json
%APPDATA%\MyApp\config.json
```

The test is whether a reader on a different machine, OS, or user account can follow the example
without rewriting it.
