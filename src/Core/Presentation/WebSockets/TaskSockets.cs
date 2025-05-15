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

        var buffer = new byte[1024];
        WebSocketReceiveResult result = null;

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
                    Console.WriteLine(
                        $"[TaskSockets] Received raw data: {data.Substring(0, Math.Min(100, data.Length))}..."
                    );

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
                        Console.WriteLine(
                            $"[ReceiveAsync] Mensagem recebida: {JsonSerializer.Serialize(response)}"
                        );
                        await publisher.PublishAsync(
                            RedisChannel.Literal("post_processing_queue"),
                            JsonSerializer.Serialize(response)
                        );
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"[TaskSockets] Error deserializing WebSocket message: {ex.Message}"
                        );
                        Console.WriteLine($"[TaskSockets] Attempting fallback deserialization...");

                        try
                        {
                            // Fallback para lidar com diferentes formatos
                            var jsonObj = JsonDocument.Parse(data).RootElement;

                            // Criar um TaskResponseData<object> em vez de TaskResponseData
                            var responseData = new TaskResponseData<object>
                            {
                                RequestId = jsonObj.TryGetProperty("id", out var idProp)
                                    ? idProp.GetString() ?? ""
                                    : "",
                                Error = jsonObj.TryGetProperty("error", out var errorProp)
                                    ? errorProp.GetString() ?? ""
                                    : "",
                                Response = jsonObj.TryGetProperty("response", out var responseProp)
                                    ? (object)responseProp
                                    : null,
                            };

                            string responseJson = JsonSerializer.Serialize(
                                new TaskResponse { NodeId = id, Data = responseData }
                            );

                            Console.WriteLine(
                                $"[TaskSockets] Fallback response: {responseJson.Substring(0, Math.Min(100, responseJson.Length))}..."
                            );
                            await publisher.PublishAsync(
                                RedisChannel.Literal("post_processing_queue"),
                                responseJson
                            );
                        }
                        catch (Exception fallbackEx)
                        {
                            Console.WriteLine(
                                $"[TaskSockets] Fallback also failed: {fallbackEx.Message}"
                            );
                        }
                    }
                }

                await webSocketNodesQueue.AddWebsocketInQueueAsync(id, webSocket);
            } while (!result.CloseStatus.HasValue);

            await webSocket.CloseAsync(
                result.CloseStatus.Value,
                result.CloseStatusDescription,
                CancellationToken.None
            );
        }
        catch (OperationCanceledException)
        {
            if (webSocket.State == WebSocketState.Open)
            {
                await webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Connection closed due to cancellation",
                    CancellationToken.None
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
                    CancellationToken.None
                );
            }
        }
    }
}
