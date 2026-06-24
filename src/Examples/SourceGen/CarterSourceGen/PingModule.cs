using Carter;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CarterSourceGen;

/// <summary>
/// A <c>public</c> Carter module. Under Needlr source generation, a public module is the
/// shape that previously double-registered: Carter's own <c>AddCarter()</c> assembly scan
/// found it AND Needlr's type registry forwarded <c>ICarterModule</c> to it, so every route
/// was mapped twice and returned an <c>AmbiguousMatchException</c> at request time.
///
/// The <c>NexusLabs.Needlr.Carter</c> plugin now calls
/// <c>AddCarter(c =&gt; c.WithEmptyModules())</c>, making Needlr the single registrar. This
/// module is therefore registered exactly once and serves <c>GET /api/ping</c> normally.
/// </summary>
public sealed class PingModule : ICarterModule
{
    /// <inheritdoc />
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/ping", (GreetingProvider greetingProvider) =>
            Results.Ok(new { message = greetingProvider.GetGreeting() }));
    }
}
