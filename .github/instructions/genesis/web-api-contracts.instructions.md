---
applyTo: "**/*CarterModule.cs,**/*Controller.cs,**/*Endpoint.cs,**/*Request.cs,**/*Response.cs"
---

# Web API Contract Rules

- The HTTP status code and response body must describe the same outcome. Never return
  a successful status for a failed operation.
- Expose stable machine-readable error codes in the project's standard error or
  `ProblemDetails` shape. Human-readable messages may change; codes must not.
- Validate body, route, query, and header inputs at the boundary. Authorization must
  also prove the caller may act on the referenced resource or tenant.
- Long-running work returns `202 Accepted` with a stable operation identifier and
  status location instead of holding the request open.
- `GET` and `HEAD` endpoints never cause externally visible side effects.
- Retry-prone mutations accept or derive a stable idempotency key and return the same
  logical outcome for repeated requests.
- Growing collections use bounded cursor pagination. Enforce a server-side maximum
  page size and make truncation or continuation explicit.
- Accept or generate a request/correlation identifier, return it to the caller, and
  attach it to logs and traces without using it as a metric dimension.
