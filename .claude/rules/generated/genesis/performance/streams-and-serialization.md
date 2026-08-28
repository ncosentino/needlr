---
# AUTO-GENERATED from .github/instructions/genesis/performance/streams-and-serialization.instructions.md — do not edit
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
# Streaming and serialization

- `PipeReader.AdvanceTo` tracks consumed and examined positions separately; never use
  buffer segments after advancing.
- Parse/format primitives directly with UTF-8/span APIs where the boundary is UTF-8.
- Use `Base64Url` for URL-safe base64.
- System.Text.Json uses generated contexts for trimmed/Native AOT paths.
- Large/streaming JSON uses `Utf8JsonReader`/`Utf8JsonWriter`; pass readers by `ref`.
- Every `IBufferWriter<T>.GetSpan/GetMemory` call advances exactly the count written.
- Consume a `ValueTask` exactly once; convert to `Task` before caching or sharing.
- Pooling async method builders require benchmark evidence.
