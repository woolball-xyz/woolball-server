using Application.Logic;
using Contracts.Constants;
using Domain.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Presentation.EndPoints;

public static class ModelsEndPoints
{
    public static void AddModelsEndPoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api/v1/");

        group.WithOpenApi();

        group.MapGet("models/{task}", handleModels).RequireRateLimiting("fixed");
        ;
    }

    public static IResult handleModels(string task)
    {
        if (AvailableModels.Names.ContainsKey(task))
        {
            return Results.Ok(AvailableModels.Names[task]);
        }
        return Results.NotFound();
    }
}
