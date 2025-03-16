using Application.Logic;
using Domain.Contracts;
using Domain.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Presentation.EndPoints;

public static class TemplateEndPoints
{
    public static void AddTemplateEndPoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api/v1/health-check");

        group.WithOpenApi();

        group.MapGet(string.Empty, healthCheck);
    }

    public static async Task<IResult> healthCheck()
    {
        return Results.Ok();
    }
}
