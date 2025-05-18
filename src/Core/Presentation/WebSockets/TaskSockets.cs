using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Application.Logic;
using Domain.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Presentation.Websockets;

public static class TaskSockets
{
    public static void AddTaskSockets(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("ws/");

        group.WithOpenApi();

        group.Map("{id}", ReceiveAsync);
    }

    public static async Task ReceiveAsync(
        HttpContext context,
        IConnectionMultiplexer redis,
        WebSocketNodesQueue webSocketNodesQueue,
        CancellationToken cancellationToken,
        string id
    )
    {
        if (string.IsNullOrEmpty(id))
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new { error = "Invalid ID." })
            );
            return;
        }

        var webSocket = await context.WebSockets.AcceptWebSocketAsync();

        await webSocketNodesQueue.AddWebsocketInQueueAsync(id, webSocket);

        var buffer = new byte[1024 * 4];
        WebSocketReceiveResult result;

        var publisher = redis.GetSubscriber();

        // Simplified ping mechanism
        _ = Task.Run(async () =>
        {
            while (webSocket.State == WebSocketState.Open)
            {
                try
                {
                    var pingMessage = new ArraySegment<byte>(Encoding.UTF8.GetBytes("ping"));
                    await webSocket.SendAsync(
                        pingMessage,
                        WebSocketMessageType.Text,
                        true,
                        CancellationToken.None
                    );
                    await Task.Delay(TimeSpan.FromSeconds(30));
                }
                catch
                {
                    // Ignore
                }
            }
        });

        try
        {
            do
            {
                string data = string.Empty;
                do
                {
                    result = await webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        cancellationToken
                    );
                    data += Encoding.UTF8.GetString(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                if (!string.IsNullOrEmpty(data))
                {
                    try
                    {
                        var responseBody = JsonSerializer.Deserialize<TaskResponseBody>(
                            data,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                        );
                        
                        var response = new TaskResponse
                        {
                            NodeId = id,
                            Data = responseBody?.Data ?? new TaskResponseData<object>(),
                        };

                        await publisher.PublishAsync(
                            RedisChannel.Literal("post_processing_queue"),
                            JsonSerializer.Serialize(response)
                        );
                    }
                    catch (Exception ex)
                    {
                        // should redistribute
                        Console.WriteLine($"[ReceiveAsync] Error processing task response: {ex.Message}");
                    }
                }

                await webSocketNodesQueue.AddWebsocketInQueueAsync(id, webSocket);
            } while (!result.CloseStatus.HasValue);

            await webSocket.CloseAsync(
                result.CloseStatus.Value,
                result.CloseStatusDescription,
                cancellationToken
            );
        }
        catch (OperationCanceledException)
        {
            if (webSocket.State == WebSocketState.Open)
            {
                await webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Connection closed due to cancellation",
                    cancellationToken
                );
            }
        }
        catch (WebSocketException)
        {
            if (webSocket.State == WebSocketState.Open)
            {
                await webSocket.CloseAsync(
                    WebSocketCloseStatus.InternalServerError,
                    "WebSocket error occurred.",
                    cancellationToken
                );
            }
        }
    }
}
