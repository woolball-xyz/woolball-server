using Application.Logic;
using Domain.Contracts;
using Domain.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Presentation.EndPoints;

public static class TasksEndPoints
{
    private readonly string[] allowedOrigins = new[] { "https://woolball-xyz.github.io" };

    public static void AddTasksEndPoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api/v1/");

        group.WithOpenApi();

        group.MapPost("{task}", handleTask).RequireAuthorization().RequireRateLimiting("fixed");
    }

    public static async Task handleTask(
        string task,
        HttpContext context,
        ITaskBusinessLogic logic,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var userId = context.User.Claims.FirstOrDefault();
            if (string.IsNullOrEmpty(userId))
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync(
                    JsonSerializer.Serialize(new { error = "Unauthorized access." })
                );
                return;
            }

            var form = await context.Request.ReadFormAsync();
            var request = TaskRequest.Create(form);

            if (request == null)
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync(
                    JsonSerializer.Serialize(new { error = "Invalid request." })
                );
                return;
            }

            if (!request.IsValidTask())
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync(
                    JsonSerializer.Serialize(new { error = "Invalid task." })
                );
                return;
            }
            var (result, error) = request.IsValidFields();
            if (!result)
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = error }));
                return;
            }
            request.RequesterId = userId;
            if (!logic.NonNegativeFunds(request))
            {
                context.Response.StatusCode = 402; // Payment Required - more appropriate for insufficient funds
                await context.Response.WriteAsync(
                    JsonSerializer.Serialize(
                        new { error = "Insufficient funds for this operation" }
                    )
                );
                return;
            }

            if (!await logic.PublishPreProcessingQueueAsync(request))
            {
                context.Response.StatusCode = 503;
                await context.Response.WriteAsync(
                    JsonSerializer.Serialize(
                        new
                        {
                            error = "Unable to process request due to queue service unavailability",
                        }
                    )
                );
                return;
            }

            var response = await logic.AwaitTaskResultAsync(request);

            context.Response.StatusCode = 200;
            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
            return;
        }
        catch (Exception e)
        {
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new { error = "internal error" })
            );
            return;
        }
    }
}
