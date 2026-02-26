using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Application.Logic;
using Domain.Contracts;
using Infrastructure.Redis;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Presentation.Websockets;

public static class TaskSockets
{
    private const int MaxMessageSize = 10 * 1024 * 1024; // 10 MB per message

    private static readonly bool WsAuthEnabled;
    private static readonly string? WsApiKey;

    static TaskSockets()
    {
        var mode = Environment.GetEnvironmentVariable("AUTH_WS_MODE") ?? "none";
        WsAuthEnabled = string.Equals(mode, "api-key-env", StringComparison.OrdinalIgnoreCase);

        if (WsAuthEnabled)
        {
            WsApiKey = Environment.GetEnvironmentVariable("WS_API_KEY");
            if (string.IsNullOrEmpty(WsApiKey))
            {
                Console.WriteLine(
                    "[Auth] WARNING: AUTH_WS_MODE=api-key-env but WS_API_KEY is not set. " +
                    "All WebSocket connections will be rejected with 4001.");
            }
        }
    }

    public static void AddTaskSockets(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("ws/");

        group.WithOpenApi();

        group.Map("{id}", ReceiveAsync)
            .RequireRateLimiting("ws-fixed");
    }

    public static async Task ReceiveAsync(
        HttpContext context,
        IConnectionMultiplexer redis,
        IRedisStreamPublisher streamPublisher,
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

        // Validate token BEFORE accepting the WebSocket connection
        if (WsAuthEnabled)
        {
            var token = context.Request.Query["token"].FirstOrDefault();
            if (string.IsNullOrEmpty(token) ||
                string.IsNullOrEmpty(WsApiKey) ||
                !string.Equals(token, WsApiKey, StringComparison.Ordinal))
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync(
                    JsonSerializer.Serialize(new { error = "Unauthorized" }));
                return;
            }
        }

        var operatorId = context.Request.Query["operator"].FirstOrDefault();

        var webSocket = await context.WebSockets.AcceptWebSocketAsync();

        await webSocketNodesQueue.AddWebsocketInQueueAsync(id, webSocket, operatorId);
        var connectionId = await webSocketNodesQueue.AddConnectionAsync(id, webSocket);

        var buffer = new byte[1024 * 4];
        WebSocketReceiveResult result;

        using var pingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var pingTask = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
            try
            {
                while (await timer.WaitForNextTickAsync(pingCts.Token))
                {
                    if (webSocket.State != WebSocketState.Open) break;
                    await webSocket.SendAsync(
                        new ArraySegment<byte>(Encoding.UTF8.GetBytes("ping")),
                        WebSocketMessageType.Text, true, pingCts.Token);
                }
            }
            catch (OperationCanceledException) { }
        }, pingCts.Token);

        try
        {
            do
            {
                var sb = new StringBuilder();
                do
                {
                    result = await webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        cancellationToken
                    );
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                    if (sb.Length > MaxMessageSize)
                    {
                        await webSocket.CloseAsync(
                            WebSocketCloseStatus.MessageTooBig,
                            "Message exceeds maximum allowed size",
                            cancellationToken
                        );
                        return;
                    }
                } while (!result.EndOfMessage);

                var data = sb.ToString();

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
                            ReceivedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        };

                        await streamPublisher.PublishAsync(
                            StreamNames.PostProcessing,
                            JsonSerializer.Serialize(response)
                        );
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"[ReceiveAsync] Error processing task response: {ex.GetType().Name}"
                        );
                    }
                }

                await webSocketNodesQueue.AddWebsocketInQueueAsync(id, webSocket, operatorId);
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
        finally
        {
            pingCts.Cancel();
            try { await pingTask; } catch (OperationCanceledException) { }
            await webSocketNodesQueue.RemoveConnectionAsync(connectionId);
        }
    }
}
