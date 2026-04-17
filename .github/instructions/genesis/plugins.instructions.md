---
applyTo: "**/*Plugin.cs"
---

# Plugin Pattern Rules

- Needlr auto-discovers and registers **ALL concrete classes** that are NOT decorated with `[DoNotAutoRegister]`.
  - This includes repositories, services, handlers, consumers, and unit-of-work types.
  - Types will get singleton lifetime scope by default from Needlr.
  - Classes will be registered as their type as well as all interfaces they implement.

## What belongs in `Configure()`

- A plugin's `Configure()` should contain **only** things Needlr cannot auto-discover.
- Needlr automatically registers types as singleton lifetime scope. If you need a different lifetime scope for some reason, you will need manual registration.

## What does NOT belong in `Configure()`

NEVER manually call for basic type registrations. Needlr already registers all of these. Doing it manually creates duplicate registrations and is an anti-pattern:

```csharp
// ❌ WRONG — this is done automatically by needlr
options.Services.AddSingleton<MyRepository>();
options.Services.AddSingleton<MyService>();
options.Services.AddSingleton<MyUnitOfWork>();
```

NEVER manually call to register as an interfaces. Needlr already registers the base type and all interfaces:

```csharp
// ❌ WRONG — this is done automatically by needlr
options.Services.AddSingleton<IMyRepository, MyRepository>();
```

NEVER manually call to register as collections or enumerables or lazy. Needlr already registers types in a way that you can resolve them WITHOUT needing to do this manually:

```csharp
// ❌ WRONG — enumerables are done automatically by needlr
options.Services.AddSingleton<IEnumerable[]>(x => [repo1, repo2]);

// ❌ WRONG — collections are done automatically by needlr
options.Services.AddSingleton<IMyRepository[]>(x => [repo1, repo2]);

// ❌ WRONG — Lazy<T> is done automatically by needlr
options.Services.AddSingleton<Lazy<IRepository>>(x => new Lazy<IRepository>(() => /* make an instance */));
```

NEVER manually wire up `IOptions<T>` when you can easily use the `[Options]` attribute from needlr:
```csharp
// ❌ WRONG — Needlr will do all of this for you if you use [Options] properly
options.Services.Configure<MyFeatureOptions>(options.Configuration.GetSection("MyFeature"));
```

NEVER manually register named `HttpClient` instances when you can use the `[HttpClientOptions]` source generator:
```csharp
// ❌ WRONG — Needlr will handle all of this via [HttpClientOptions] on a record
options.Services.AddHttpClient("MyService", client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
    client.BaseAddress = new Uri("https://api.myservice.com/");
});
```

## Plugin class itself

`IServiceCollectionPlugin` implementations are **automatically excluded** from Needlr's auto-registration — `[DoNotAutoRegister]` is **NOT needed** and must not be added:

```csharp
// ✅ CORRECT — no [DoNotAutoRegister]
internal sealed class MyFeaturePlugin : IServiceCollectionPlugin
{
    public void Configure(ServiceCollectionPluginOptions options)
    {
	    /* do your registrations here */
    }
}

// ❌ WRONG — redundant attribute, do not add
[DoNotAutoRegister]
internal sealed class MyFeaturePlugin : IServiceCollectionPlugin { ... }
```

This is the same for other Needlr-based plugins. You do NOT need to mark them with `[DoNotAutoRegister]` since their interfaces are already marked as such.

There are dedicated Needlr plugin registration patterns including (but not limited to):
- `IWebApplicationPlugin`: which has `public void Configure(WebApplicationPluginOptions options)`

## If there is nothing to configure

If a plugin class has no manual registrations required, it does not need to exist and should be deleted.