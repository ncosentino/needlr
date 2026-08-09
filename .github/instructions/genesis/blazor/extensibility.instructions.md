---
applyTo: "**/*.razor,**/*.razor.cs"
---

# Blazor extensibility

- Use `RenderFragment` for projected content and named regions.
- Use `[EditorRequired] RenderFragment<T>` with an item collection for caller-supplied
  item templates.
- Use `@typeparam` for reusable compile-time generic components.
- Use `DynamicComponent` only when type selection is genuinely runtime-driven.
  Trimming/AOT shapes prefer a finite concrete switch/dictionary or build-time
  discovery.
- Use named cascading values for true cross-cutting state. Ordinary data flow remains
  explicit parameters or DI.
- Keep cascading dependencies sparse because they are implicit to the component API.
