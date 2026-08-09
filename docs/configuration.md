---
description: How Needlr selects discovery, registration, and generated configuration behavior.
---

# Configuration

Needlr does not expose one global configuration object. Applications compose behavior
through the fluent `Syringe` API and opt into one discovery strategy:

- `.UsingSourceGen()` for compile-time discovery;
- `.UsingReflection()` for explicit runtime discovery;
- `.UsingAutoConfiguration()` from the bundle package when fallback behavior is
  intentionally desired.

Assembly providers, type filters, registrars, plugins, and web/host builders are
selected through the same fluent composition boundary. See
[Core Concepts](core-concepts.md) for the runtime model.

Generated application settings use Microsoft.Extensions.Options:

- `[Options]` binds an options type to a configuration section;
- `[HttpClientOptions]` binds options and registers a named `HttpClient`;
- host and web builders use their normal `IConfiguration`;
- non-host applications must pass `IConfiguration` to
  `BuildServiceProvider(configuration)` when generated options need external values.

See [Options Binding](options.md) and
[Named HttpClient Registration](http-clients.md) for the exact attributes, defaults,
validation rules, and examples.
