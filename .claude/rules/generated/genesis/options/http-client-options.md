---
# AUTO-GENERATED from .github/instructions/genesis/options/http-client-options.instructions.md — do not edit
paths:
  - "**/*HttpClientOptions.cs"
---
# HttpClient options

- Use a `public sealed record` with `[HttpClientOptions]`.
- Implement `INamedHttpClientOptions` (`NDLRHTTP001`). Prefer
  `IStandardHttpClientOptions` for timeout, user agent, base address, and default
  headers; otherwise implement only required capability interfaces.
- Choose one compile-time client-name source in this precedence:
  `[HttpClientOptions(Name = "...")]`, literal `ClientName`, then the type name with
  `HttpClientOptions` removed.
- Conflicting name sources fail `NDLRHTTP002`; computed `ClientName` fails
  `NDLRHTTP003`; duplicate resolved names fail `NDLRHTTP005`.
- Prefer an explicit feature-owned configuration section. The fallback is
  `HttpClients:<ResolvedName>`.
- Property initializers provide defaults; configuration overrides only environment
  differences.
- Needlr emits options binding and named-client registration. Do not add a duplicate
  `AddHttpClient` call in a plugin.
