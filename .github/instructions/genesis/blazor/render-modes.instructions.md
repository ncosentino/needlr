---
applyTo: "**/*.razor,**/*.razor.cs"
---

# Blazor Render Mode Rules

These rules govern how render modes are applied to Blazor components. Render modes determine
where a component executes (server or client) and whether it supports interactivity.

## Render mode reference

| Mode | Directive | Location | Interactive | Use when |
|------|-----------|----------|-------------|----------|
| Static SSR | (none) | Server | No | Content pages, SEO-critical pages |
| Interactive Server | `@rendermode InteractiveServer` | Server | Yes | Admin dashboards, internal tools |
| Interactive WebAssembly | `@rendermode InteractiveWebAssembly` | Client | Yes | Offline-capable, client-heavy UI |
| Interactive Auto | `@rendermode InteractiveAuto` | Server then Client | Yes | Best of both — fast first load, then client |

## Choosing a render mode

- **Default to Static SSR** for content that does not need interactivity
- **Use Interactive Server** when you need server resources (DB, file system) and low latency is
  acceptable (requires persistent SignalR connection)
- **Use Interactive WebAssembly** when the component must work offline or you want to reduce
  server load (requires downloading the .NET runtime to the browser)
- **Use Interactive Auto** when you want fast initial load (server) with subsequent client-side
  rendering after the WebAssembly bundle downloads

## Apply render modes at the right level

- **Per-component:** Use `@rendermode` directive on the component definition for routable pages
- **Per-instance:** Use `@rendermode` attribute when embedding a component: `<Dialog @rendermode="InteractiveServer" />`
- **Global:** Set on the `Routes` component in `App.razor` — only when the entire app uses one mode

Prefer per-component over global. Mixing render modes is a Blazor strength — use static SSR for
content pages and interactive modes only where needed.

## Prerendering

Prerendering is enabled by default for interactive components. Be aware of its implications:

- `OnInitializedAsync` runs **twice** during prerendering (once on server, once when interactive)
- Use `RendererInfo.IsInteractive` to guard interactive-only code:

```csharp
protected override async Task OnInitializedAsync()
{
    // This runs during both prerender and interactive phases
    Products = await ProductService.GetAllAsync();
}

protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender && RendererInfo.IsInteractive)
    {
        // JS interop is only available after interactive render
        await JS.InvokeVoidAsync("initializeChart", chartElement);
    }
}
```

## Render mode and DI scope implications

- **Static SSR:** Services are scoped to the HTTP request
- **Interactive Server:** Services are scoped to the SignalR circuit (longer-lived than HTTP)
- **Interactive WebAssembly:** `Scoped` services behave as `Singleton` (no DI scope concept)
- **Interactive Auto:** First render uses server scoping, subsequent renders use client scoping

Design services to work correctly regardless of scope lifetime. Avoid storing mutable state in
scoped services that may outlive a single user interaction.

## Components must not assume their render mode

Do not couple component logic to a specific render mode. A well-designed component should work
in any mode. Use `RendererInfo` to detect the current mode when you need conditional behavior,
but prefer designing components that degrade gracefully when rendered statically.
