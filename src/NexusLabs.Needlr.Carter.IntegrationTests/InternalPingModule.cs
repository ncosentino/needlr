using Carter;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace NexusLabs.Needlr.Carter.IntegrationTests;

/// <summary>
/// An <c>internal</c> Carter module. Carter 10.0.0 only discovers public modules, so its
/// <c>AddCarter()</c> scan skips this one — Needlr's type registry is its sole registrar.
/// This is the case a blanket "exclude ICarterModule from Needlr" fix would have broken.
/// </summary>
internal sealed class InternalPingModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/internal-ping", () => Results.Ok(new { message = "internal-pong" }));
    }
}
