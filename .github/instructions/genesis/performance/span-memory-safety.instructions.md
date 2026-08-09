---
applyTo: "**/*Service.cs,**/*Repository.cs,**/*Worker.cs,**/*Job.cs,**/*Consumer.cs,**/*CarterModule.cs,**/*Client.cs,**/*Handler.cs,**/*UnitOfWork.cs,**/*Parser.cs,**/*Serializer.cs,**/*Reader.cs,**/*Writer.cs,**/*Stream.cs,**/*Buffer.cs"
---

# Span and memory safety

- `Span<T>`/`ReadOnlySpan<T>` stay synchronous and stack-bound; stored or awaited
  data uses `Memory<T>`/`ReadOnlyMemory<T>`.
- Do not resize/mutate a collection while holding a `CollectionsMarshal` span/ref.
- `MemoryMarshal` escape hatches require explicit size/alignment proof.
- Caller-influenced `stackalloc` sizes have a ceiling and pooled/heap fallback.
- `[SkipLocalsInit]` is allowed only when every byte is initialized before reading.
- Pin only for native interop and release every pin in `finally`.
