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

    public static async Task<IResult> ReceiveAsync(
        HttpContext context,
        IConnectionMultiplexer redis,
        WebSocketNodesQueue webSocketNodesQueue,
        CancellationToken cancellationToken,
        string id
    )
    {
        if (string.IsNullOrEmpty(id))
        {
            return Results.NotFound();
        }

        var webSocket = await context.WebSockets.AcceptWebSocketAsync();

        // Adicionar o WebSocket ao gerenciador de conexões
        await webSocketNodesQueue.AddWebsocketInQueueAsync(id, webSocket);

        var buffer = new byte[1024];
        WebSocketReceiveResult result;

        var publisher = redis.GetSubscriber();
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

                // Processar a mensagem recebida
                if (!string.IsNullOrEmpty(data))
                {
                    // Publicar a mensagem recebida para processamento
                    var responseData = JsonSerializer.Deserialize<TaskResponseData>(data);
                    var response = new TaskResponse { NodeId = id, Data = responseData };

                    await publisher.PublishAsync(
                        "post_processing_queue",
                        JsonSerializer.Serialize(response)
                    );
                }
            } while (!result.CloseStatus.HasValue);
        }
        catch (WebSocketException)
        {
            await webSocket.CloseAsync(
                WebSocketCloseStatus.InternalServerError,
                "WebSocket error occurred.",
                CancellationToken.None
            );
        }

        return Results.Ok();
    }
}
