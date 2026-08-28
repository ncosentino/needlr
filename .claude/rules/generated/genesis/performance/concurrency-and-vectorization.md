---
# AUTO-GENERATED from .github/instructions/genesis/performance/concurrency-and-vectorization.instructions.md — do not edit
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
# Concurrency and vectorization

- New synchronous locks use `System.Threading.Lock` with the field typed as `Lock`.
  Never await while its scope is held.
- Choose bounded-channel full mode deliberately; dropping is allowed only when data
  loss is part of the contract.
- Periodic background work uses `PeriodicTimer`, not a delay loop.
- SIMD paths guard hardware support and retain a scalar fallback. Prefer
  `TensorPrimitives` before hand-vectorizing.
- Do not add inlining, aggressive optimization, cache-line padding, or no-GC regions
  without a benchmark and a documented latency/allocation target.
