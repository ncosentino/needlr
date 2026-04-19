---
applyTo: "**/*.razor"
---

# Blazor Component Rules

These rules apply to all Razor component files (`.razor`). They cover structure, conventions,
and patterns for building modular, testable Blazor components.

## File structure and naming

- Component files use PascalCase: `ProductDetail.razor`, not `productDetail.razor`
- Routable page components live in a `Pages/` folder within their Feature project
- Shared/reusable components live alongside the feature that owns them or in a shared library
- One component per `.razor` file — do not define multiple components in a single file

## Directive ordering

Place directives at the top of the component in this order, with no blank lines between them:

```razor
@page "/product-detail/{Id:int}"
@rendermode InteractiveServer
@using System.Globalization
@using Microsoft.AspNetCore.Components
@attribute [Authorize]
@implements IAsyncDisposable
@inject IProductService ProductService
@inject ILogger<ProductDetail> Logger

<PageTitle>Product Detail</PageTitle>
```

Order: `@page` → `@rendermode` → `@using` → `@attribute` → `@implements` / `@inherits` →
`@inject` → blank line → markup.

## Prefer code-behind over inline `@code`

For any component with more than trivial logic, use a code-behind partial class in a separate
`.razor.cs` file. This improves testability, keeps markup clean, and enables proper constructor
injection via Needlr:

```
ProductDetail.razor        ← markup only
ProductDetail.razor.cs     ← partial class with logic
```

The code-behind class inherits from `ComponentBase` (implicit) and uses the same namespace as the
`.razor` file.

## Parameters

- Use `[Parameter]` for parent-to-child data flow
- Use `[CascadingParameter]` only for cross-cutting concerns (theme, auth state, layout context)
- Never mutate a `[Parameter]` value inside the component — treat parameters as read-only inputs
- Use `[EditorRequired]` for parameters that must be supplied by the parent:

```csharp
[Parameter, EditorRequired]
public int ProductId { get; set; }
```

## EventCallback for child-to-parent communication

Use `EventCallback<T>` for child-to-parent communication, never raw delegates or static events:

```csharp
[Parameter]
public EventCallback<Product> OnProductSelected { get; set; }
```

## Component disposal

Implement `IAsyncDisposable` when the component holds subscriptions, timers, or JS interop
references. Dispose in `DisposeAsync`, not in lifecycle methods:

```csharp
@implements IAsyncDisposable

@code {
    public async ValueTask DisposeAsync()
    {
        // Cancel timers, unsubscribe events, dispose JS references
    }
}
```
