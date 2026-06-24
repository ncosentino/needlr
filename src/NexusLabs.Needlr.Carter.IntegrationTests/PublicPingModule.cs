using Carter;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace NexusLabs.Needlr.Carter.IntegrationTests;

/// <summary>
/// A <c>public</c> Carter module. It is discovered by BOTH Carter's <c>AddCarter()</c> scan
/// (which finds public modules) AND Needlr's source-generated type registry, so it is the
/// shape that previously double-registered.
/// </summary>
public sealed class PublicPingModule : ICarterModule
{
    /// <inheritdoc />
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/public-ping", () => Results.Ok(new { message = "public-pong" }));
    }
}
