---
applyTo: "**/*Repository.cs,**/*UnitOfWork.cs"
---

# Transaction Lock Ordering

- Every transactional path that touches the same resources must acquire locks in the same order;
  never let sibling paths lock parent and child resources in opposite orders.
- Acquire batch locks in deterministic key order.
- When row locks are required, acquire them explicitly in the established order rather than relying
  on incidental query order.
