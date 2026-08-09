---
applyTo: "**/*Client.cs,**/*HttpClientOptions.cs"
---

# HTTP clients

- Never construct/inject `HttpClient` directly. Inject `IHttpClientFactory` and create
  the named client registered by `[HttpClientOptions]`.
- The `CreateClient` name exactly matches the options type's compile-time resolved
  name.
- Pass `CancellationToken` to every async HTTP/content operation.
- Dispose factory-created clients after use; the factory owns pooled handlers.
- Client classes translate one external API boundary. They do not own business rules.
- Encode path/query values and validate status/content before mapping the response.
- Runtime defaults live on the options record; environment overrides live in
  configuration.
- Tests use a controlled `HttpMessageHandler` behind `IHttpClientFactory`; test client
  behavior, not Needlr's generated registration.
