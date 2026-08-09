---
applyTo: "**/*.cs"
---

# File and type isolation

- Every public or internal top-level type has its own file named exactly after the
  type. Do not co-locate an interface and implementation.
- Private nested types are allowed only when they never escape the defining class.
- Private external-response DTOs may stay inside one HTTP client; private row-mapping
  DTOs may stay inside one repository.
- Small request/response records may share the Carter module that exclusively owns
  them.
- If a type appears in a public/internal member signature or is reused elsewhere,
  extract it to its own file.
