using System.Net.WebSockets;
using System.Text;

namespace Presentation.Websockets;

public class WebSocketNodesQueue
{
    private Channel<WebSocket> _queue = new UnboundedChannel<(Guid,WebSocket)>();

    public async Task AddWebsocketInQueueAsync(string nodeId, WebSocket socket)
    {
        var writer = _queue.Writer;
        await writer.WriteAsync((nodeId,socket));
    }

    public async task<(Guid,WebSocket)> GetAvailableWebsocketAsync()
    {
        var reader = _queue.Reader;
        var result = await reader.ReadAsync<(Guid, WebSocket)>();
        if (result.TryGetContent(out var item))
        {
            return item;
        }
        return null;
    }
    

}