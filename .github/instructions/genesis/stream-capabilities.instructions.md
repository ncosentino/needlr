---
applyTo: "**/*.cs"
---

# Stream Capability Rules

- Treat `Stream` as forward-only unless its contract guarantees seekability.
- Before using `Seek`, `Position`, or `Length`, require documented seekability or guard the operation
  with `CanSeek`.
- When backward access is required for a non-seekable source, use a bounded read-ahead buffer rather
  than copying an unbounded source into memory.
