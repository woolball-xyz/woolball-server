using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Presentation.Websockets;

public class WebSocketNodesQueue
{
    private Channel<(string, WebSocket)> _queue = Channel.CreateUnbounded<(string, WebSocket)>();

    public async Task AddWebsocketInQueueAsync(string nodeId, WebSocket socket)
    {
        await _queue.Writer.WriteAsync((nodeId, socket));
    }

    public async Task<(string?, WebSocket?)> GetAvailableWebsocketAsync()
    {
        while (await _queue.Reader.WaitToReadAsync())
        {
            while (_queue.Reader.TryRead(out var item))
            {
                var (groupName, webSocket) = item;

                if (webSocket?.State == WebSocketState.Open)
                {
                    return item;
                }
            }
        }

        // If we get here, the channel is empty and no available WebSocket was found
        return (null, null);
    }
}
