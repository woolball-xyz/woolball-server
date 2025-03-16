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
        var group = app.MapGroup("ws/");

        group.WithOpenApi();

        group.Map("{id}", ReceiveAsync);
    }



    public static async Task<IResult> ReceiveAsync(
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
