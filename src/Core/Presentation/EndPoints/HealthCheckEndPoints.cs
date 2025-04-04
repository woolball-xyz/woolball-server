using Application.Logic;
using Domain.Contracts;
using Domain.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Presentation.EndPoints;

public static class HealthCheckEndPoints
{
    public static void AddHealthCheckEndPoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api/v1/health-check");

        group.WithOpenApi();

        group.MapGet(string.Empty, HealthCheck).RequireRateLimiting("fixed");
        ;
    }

    public static async Task<IResult> HealthCheck()
    {
        return Results.Ok();
    }
}
