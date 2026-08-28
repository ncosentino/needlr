---
# AUTO-GENERATED from .github/instructions/genesis/performance/data-scanning-and-lookups.instructions.md — do not edit
paths:
  - "**/*Service.cs"
  - "**/*Repository.cs"
  - "**/*Worker.cs"
  - "**/*Job.cs"
  - "**/*Consumer.cs"
  - "**/*CarterModule.cs"
  - "**/*Client.cs"
  - "**/*Handler.cs"
  - "**/*UnitOfWork.cs"
  - "**/*Parser.cs"
  - "**/*Serializer.cs"
  - "**/*Reader.cs"
  - "**/*Writer.cs"
  - "**/*Stream.cs"
  - "**/*Buffer.cs"
---
# Data scanning and lookups

- Build `SearchValues<T>`, `FrozenDictionary`, `FrozenSet`, and `CompositeFormat` once
  and reuse them; never construct them on a repeated path.
- Frozen collections are for trusted startup-built data, not untrusted input.
- Use alternate span lookup instead of allocating a key string when the comparer
  supports it.
- Prefer `IUtf8SpanFormattable`/`IUtf8SpanParsable<T>` for direct UTF-8 boundaries.
- Do not reference withdrawn `Utf8String`/`Utf8Span` APIs.
