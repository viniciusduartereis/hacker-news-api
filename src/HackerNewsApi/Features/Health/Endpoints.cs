using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace HackerNewsApi.Features.Health;

public static class Endpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/health");
        group.WithTags("Health");

        group.MapGet("", () => Results.Ok(new { status = "ok", timestamp = DateTime.UtcNow }))
            .WithName("HealthCheck")
            .Produces(StatusCodes.Status200OK);
    }
}
