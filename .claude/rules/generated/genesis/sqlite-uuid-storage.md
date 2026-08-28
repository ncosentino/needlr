---
# AUTO-GENERATED from .github/instructions/genesis/sqlite-uuid-storage.instructions.md — do not edit
paths:
  - "**/*Repository.cs"
---
# SQLite UUID storage

When a repository persists UUIDs in SQLite:

- Choose one database-wide representation: canonical 36-character text or 16-byte
  blob. Never mix them.
- Route every read, write, key, index, and query parameter through one codec; never
  rely on driver-default `Guid` binding.
- Blob storage pins byte order. UUIDv7/time-ordered values use big-endian RFC field
  order, not `Guid.ToByteArray()`'s legacy mixed-endian layout.
- State the representation beside the schema/codec.
- Cover a known-value round trip with non-zero high bytes so an endianness swap cannot
  pass silently.
