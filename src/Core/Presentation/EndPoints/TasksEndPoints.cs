using System.Text;
using System.Text.Json;
using Application.Logic;
using Contracts.Constants;
using Domain.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Presentation.EndPoints;

public static class TasksEndPoints
{
    public static void AddTasksEndPoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api/v1/");

        group.WithOpenApi();

        group.MapPost("{task}", handleTask).RequireRateLimiting("fixed");
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
            var form = await context.Request.ReadFormAsync();
            var request = await TaskRequest.Create(form, task);

            if (request == null)
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync(
                    JsonSerializer.Serialize(new { error = "Invalid request." })
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

            bool isStreaming =
                request.Kwargs.ContainsKey("stream")
                && request.Kwargs["stream"].ToString().ToLower() == "true";

            if (isStreaming)
            {
                context.Response.ContentType = "text/plain";

                await foreach (
                    var message in logic.StreamTaskResultAsync(request, cancellationToken)
                )
                {
                    var bytes = System.Text.Encoding.UTF8.GetBytes(message + "\n");
                    await context.Response.Body.WriteAsync(bytes, 0, bytes.Length);
                    await context.Response.Body.FlushAsync();
                }

                context.Response.Body.Close();
            }
            else
            {
                var response = await logic.AwaitTaskResultAsync(request);
                if (!string.IsNullOrEmpty(response))
                {
                    if (
                        response.Contains("\"Status\":\"Error\"") || response.Contains("\"error\":")
                    )
                    {
                        context.Response.StatusCode = 500;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync(response);
                    }
                    else
                    {
                        context.Response.StatusCode = 200;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync(response);
                    }
                }
                else
                {
                    context.Response.StatusCode = 500;
                    await context.Response.WriteAsync(
                        JsonSerializer.Serialize(
                            new { error = "Could not get response from service" }
                        )
                    );
                }
            }
            return;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new { error = "internal error" })
            );
            return;
        }
    }
}
