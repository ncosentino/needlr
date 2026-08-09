---
applyTo: "**/*Service.cs,**/*Repository.cs,**/*Worker.cs,**/*Job.cs,**/*Consumer.cs,**/*CarterModule.cs,**/*Client.cs,**/*Handler.cs,**/*UnitOfWork.cs,**/*Parser.cs,**/*Serializer.cs,**/*Reader.cs,**/*Writer.cs,**/*Stream.cs,**/*Buffer.cs"
---

# Buffer and object pooling

- Repeated/hot paths rent reusable buffers instead of allocating per operation.
- Prefer `RentSpan` for synchronous stack-bound work and `RentMemory` across `await`;
  dispose the one owner exactly once.
- Do not copy/pass a `RentSpan` owner, use a rented buffer after disposal, assume the
  returned capacity equals the request, or share mutable contents without
  synchronization. `NLF0024` rejects double-owner patterns.
- Read the granted capacity from the owner; rent size is a minimum, not an exact
  allocation.
- Sensitive contents are zeroed before return; `clearOnReturn` alone is insufficient
  when a full pool drops an array.
- Use a dedicated pool for very large or secret-bearing buffers when retention must be
  isolated from unrelated callers.
- Raw `ArrayPool<T>` requires `try/finally` and return to the same pool.
- Scope `IMemoryOwner<T>` with `using`; its memory cannot outlive the owner.
- Object-pool policies reset all state before reuse, and leases are single-threaded.
- Use `RecyclableMemoryStream` for large/frequent streams; avoid `ToArray()` and bound
  retained pool bytes.
