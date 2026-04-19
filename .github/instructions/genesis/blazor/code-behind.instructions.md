---
applyTo: "**/*.razor.cs"
---

# Blazor Code-Behind Rules

These rules apply to Blazor component code-behind files (`.razor.cs`).

## Class declaration

Code-behind classes are `partial` and match the component name. They do not explicitly inherit
from `ComponentBase` unless overriding lifecycle methods — the `.razor` file already implies it:

```csharp
namespace TemplateProject.Features.Products;

public sealed partial class ProductDetail
{
    [Inject]
    private IProductService ProductService { get; set; } = default!;

    [Parameter, EditorRequired]
    public int ProductId { get; set; }
}
```

## Dependency injection

Use `[Inject]` on properties in code-behind files. This is equivalent to `@inject` in the `.razor`
file but keeps DI declarations in C# where they are testable and type-checked:

```csharp
[Inject]
private IProductService ProductService { get; set; } = default!;

[Inject]
private ILogger<ProductDetail> Logger { get; set; } = default!;
```

Assign `= default!` to suppress nullable warnings — the DI container guarantees initialization.

## Needlr and AoT/trimming compatibility

When using Needlr for DI registration in Blazor apps targeting AoT or trimming:

- Register services via `IServiceCollectionPlugin` implementations (Needlr discovers them)
- Avoid reflection-based service resolution — use source-generated registration
- Do not use `[DynamicDependency]` or `[DynamicallyAccessedMembers]` unless required by
  third-party libraries that are not trim-safe
- Prefer concrete types over `dynamic` or `object` in component parameters to preserve
  trim safety

## Lifecycle methods

Override only the lifecycle methods you need. Prefer async versions:

| Method | Use when |
|--------|----------|
| `OnInitializedAsync` | Loading data on first render (called once) |
| `OnParametersSetAsync` | Reacting to parameter changes (called on each render with new params) |
| `OnAfterRenderAsync` | JS interop or DOM manipulation (called after each render) |
| `ShouldRender` | Performance — skip re-renders when state hasn't changed |

**Critical:** Never call `StateHasChanged()` inside `OnInitializedAsync` or `OnParametersSetAsync`
— the framework already triggers a render after these methods complete.

## Source-generated logging

Use `[LoggerMessage]` partial methods (same pattern as all other genesis projects):

```csharp
public sealed partial class ProductDetail
{
    [Inject]
    private ILogger<ProductDetail> Logger { get; set; } = default!;

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Loading product {ProductId}")]
    private partial void LogLoadingProduct(int productId);
}
```

## Testability

Design components for bUnit testing:

- Extract business logic into injected services — keep components thin
- Prefer `EventCallback<T>` over internal state mutations for verifiable behavior
- Avoid static state or ambient singletons — everything through DI
- Use `[EditorRequired]` so tests are forced to provide required parameters
