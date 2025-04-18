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
        var writer = _queue.Writer;
        await writer.WriteAsync((nodeId, socket));
    }

    public async Task<(string, WebSocket)> GetAvailableWebsocketAsync()
    {
        var reader = _queue.Reader;
        var item = await reader.ReadAsync();
        return item;
    }

    public Task RemoveClientAsync(string nodeId) { return Task.CompletedTask; }
}
