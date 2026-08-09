# Data access and repositories

Needlr deliberately does not define persistence, repository, transaction, cache, or
database-provider abstractions. Those concerns belong to the consuming application or
to a dedicated library that owns the storage boundary.

Consumer repositories are ordinary services. Needlr can discover and register them
through the same source-generated or reflection-based conventions used for other
services; it does not change their query, mapping, transaction, or lifetime semantics.

## Package boundary

Core Needlr, generator, analyzer, and framework-integration packages must not acquire an
ORM or database-provider dependency merely to support a consumer architecture. A new
persistence integration requires its own package, explicit public contract, tests, and
documentation.

Tests in this repository verify registration and composition behavior. Provider-specific
correctness remains the responsibility of the package or application that owns that
provider.
