---
applyTo: "**/*.razor,**/*.razor.cs"
---

# Blazor Extensibility Rules

These rules cover patterns for building reusable, composable, and extensible Blazor components.

## RenderFragment for content projection

Use `RenderFragment` parameters to let parent components project content into child components.
This is Blazor's equivalent of Angular's `ng-content` or React's `children`:

```csharp
[Parameter]
public RenderFragment? ChildContent { get; set; }

[Parameter]
public RenderFragment? Header { get; set; }

[Parameter]
public RenderFragment? Footer { get; set; }
```

```razor
<div class="card">
    @if (Header is not null)
    {
        <div class="card-header">@Header</div>
    }
    <div class="card-body">@ChildContent</div>
    @if (Footer is not null)
    {
        <div class="card-footer">@Footer</div>
    }
</div>
```

## RenderFragment<T> for templated components

Use `RenderFragment<TItem>` when the child needs access to an item from the parent's data:

```csharp
[Parameter, EditorRequired]
public RenderFragment<TItem> ItemTemplate { get; set; } = default!;

[Parameter, EditorRequired]
public IReadOnlyList<TItem> Items { get; set; } = [];
```

```razor
@foreach (var item in Items)
{
    @ItemTemplate(item)
}
```

## Generic components

Use `@typeparam` to create generic, type-safe components:

```razor
@typeparam TItem

<ul>
    @foreach (var item in Items)
    {
        <li>@ItemTemplate(item)</li>
    }
</ul>

@code {
    [Parameter, EditorRequired]
    public IReadOnlyList<TItem> Items { get; set; } = [];

    [Parameter, EditorRequired]
    public RenderFragment<TItem> ItemTemplate { get; set; } = default!;
}
```

Generic components are AoT and trim-safe because the type parameter is resolved at compile time.

## DynamicComponent for plugin architectures

Use `DynamicComponent` when the component type is determined at runtime:

```razor
<DynamicComponent Type="@componentType" Parameters="@parameters" />
```

**Caution:** `DynamicComponent` uses reflection and may not be trim-safe. When AoT/trimming is
required, prefer a switch/dictionary pattern with concrete types or use Needlr's plugin
discovery to resolve component types at build time.

## CascadingValue for cross-cutting state

Use `CascadingValue` to provide shared state (theme, auth, layout) without prop drilling.
Define a named cascading value to avoid ambiguity:

```razor
<CascadingValue Value="@theme" Name="AppTheme">
    @ChildContent
</CascadingValue>
```

```csharp
[CascadingParameter(Name = "AppTheme")]
private Theme? Theme { get; set; }
```

Use cascading parameters sparingly — they make component dependencies implicit. Prefer explicit
`[Parameter]` or DI for most data flow.
