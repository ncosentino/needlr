# HTTP clients and options

Needlr source generation can bind typed options and register named `HttpClient`
instances without a handwritten registration plugin. The consumer-facing
[Options Binding](../options.md) and
[Named HttpClient Registration](../http-clients.md) pages are the canonical API
references.

## General options

`[Options]` uses the options type name as the default configuration section. Consumers
can provide an explicit section path and optional name when configuration belongs to a
larger hierarchy or when multiple instances share one type.

The generated registration uses Microsoft.Extensions.Options. Validation can use data
annotations, a supported validation method, `IOptionsValidator<T>`, or the separate
FluentValidation adapter package.

Consumers choose the options interface by reload behavior:

- `IOptions<T>` for stable singleton configuration;
- `IOptionsSnapshot<T>` for request/scoped reload;
- `IOptionsMonitor<T>` for long-lived consumers that observe changes.

## Named HttpClient generation

`[HttpClientOptions]` combines options binding with named `HttpClient` registration.
The default section is `HttpClients:<ResolvedName>`, and capability interfaces control
which client properties the generator emits.

Client names and section paths are compile-time contracts. Conflicting or
non-deterministic names are analyzer errors rather than runtime fallbacks.

Non-host applications must pass an `IConfiguration` to `BuildServiceProvider(...)`;
the parameterless overload intentionally supplies an empty configuration. Host and web
application builders provide their normal configuration pipeline automatically.
