using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Application.Logic;
using Domain.Contracts;
using Domain.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Queue;

namespace Presentation.Websockets;

public static class TemplateSockets
{
    public static void AddTemplateSockets(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("ws/live");

        group.WithOpenApi();

        group.Map("thumb/{id}", ProcessaThumbnail);
        group.Map("transmit/{id}", ProcessaVideo);
    }

    public static async Task<IResult> ProcessaVideo(
        HttpContext context,
        IMessagePublisher publisher,
        string id
    )
    {
        if (string.IsNullOrEmpty(id))
        {
            return Results.NotFound();
        }

        var webSocket = await context.WebSockets.AcceptWebSocketAsync();

        try
        {
            WebSocketReceiveResult result;
            do
            {
                var buffer = new byte[1024 * 50];
                using var memoryStream = new MemoryStream();
                do
                {
                    result = await webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        CancellationToken.None
                    );
                    await memoryStream.WriteAsync(buffer.AsMemory(0, result.Count));
                } while (!result.EndOfMessage);

                memoryStream.Seek(0, SeekOrigin.Begin);

                var content = new LiveChunkMessage(id, memoryStream.ToArray());

                await publisher.PublishAsync("LiveStreamingQueue", content);
                Console.WriteLine("send video chunk {0}", id.ToString());
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

    public static async Task<IResult> ProcessaThumbnail(
        HttpContext context,
        IMessagePublisher publisher,
        string id
    )
    {
        if (string.IsNullOrEmpty(id))
        {
            return Results.NotFound();
        }

        var webSocket = await context.WebSockets.AcceptWebSocketAsync();

        var buffer = new byte[1024];
        WebSocketReceiveResult result;

        try
        {
            do
            {
                string data = string.Empty;
                do
                {
                    result = await webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        CancellationToken.None
                    );
                    data += Encoding.UTF8.GetString(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                // new message received in data

           
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
