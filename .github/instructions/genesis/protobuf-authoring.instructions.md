---
applyTo: "**/*.proto"
---

# Protobuf Schema Compatibility

- Treat field numbers as permanent. Never renumber a field or reuse a removed field number.
- When removing a field, reserve both its number and name so later edits cannot reinterpret existing
  persisted or in-flight data.
