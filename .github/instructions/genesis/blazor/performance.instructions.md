---
applyTo: "**/*.razor,**/*.razor.cs"
---

# Blazor Performance Rules

These rules cover performance patterns for Blazor components. They apply to all render modes.

## Minimize re-renders

- Override `ShouldRender()` to return `false` when component state has not changed
- Use `@key` on repeated elements to help the diffing algorithm identify stable items:

```razor
@foreach (var item in Items)
{
    <ItemCard @key="item.Id" Item="@item" />
}
```

- Avoid unnecessary `StateHasChanged()` calls — the framework handles re-renders after event
  handlers and lifecycle methods automatically

## Virtualization for large lists

Use `<Virtualize>` instead of `@foreach` for lists with more than ~50 items. The component only
renders visible items, dramatically reducing DOM size:

```razor
<Virtualize Items="allProducts" Context="product">
    <ProductCard @key="product.Id" Product="@product" />
</Virtualize>
```

For server-side data, use `ItemsProvider` to fetch only the visible slice:

```razor
<Virtualize ItemsProvider="LoadProducts" Context="product">
    <ProductCard @key="product.Id" Product="@product" />
</Virtualize>
```

## Streaming rendering for slow data

Use `[StreamRendering]` on components that load data asynchronously. The component renders
immediately with a placeholder, then updates when data arrives:

```csharp
@attribute [StreamRendering]

@if (Products is null)
{
    <LoadingSpinner />
}
else
{
    @foreach (var product in Products)
    {
        <ProductCard Product="@product" />
    }
}
```

This eliminates the blank-screen wait during `OnInitializedAsync`.

## Avoid allocations in render paths

- Do not create new objects, lists, or delegates in the markup section — these cause unnecessary
  re-renders and GC pressure
- Cache `RenderFragment` instances when the content does not change:

```csharp
// ❌ WRONG — creates a new delegate every render
<button @onclick="@(() => HandleClick(item.Id))">Click</button>

// ✅ BETTER — use a method that looks up the item
<button @onclick="HandleClick" data-id="@item.Id">Click</button>
```

## AoT and trimming for WebAssembly

When targeting AoT compilation or trimming (Blazor WebAssembly, Blazor Hybrid):

- Avoid reflection-based patterns (use source generators instead)
- Mark types used in JS interop with `[JsonSerializable]` for System.Text.Json source gen
- Use Needlr source-generated DI registration instead of assembly-scanning
- Test with `PublishTrimmed=true` regularly to catch trim warnings early
- Prefer generic type parameters over `object` or `dynamic` in component APIs

## JS interop performance

- Use `IJSRuntime.InvokeVoidAsync` when you do not need a return value
- Use `IJSObjectReference` for module-scoped JS instead of global functions
- Dispose `IJSObjectReference` in `DisposeAsync` to prevent memory leaks
- Batch JS calls when possible — each interop call has marshaling overhead
