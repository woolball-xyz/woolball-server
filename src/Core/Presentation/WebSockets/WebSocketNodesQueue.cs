using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Presentation.Websockets;

public class WebSocketNodesQueue
{
    private readonly Channel<(string, WebSocket, string?)> _queue = Channel.CreateBounded<(string, WebSocket, string?)>(
        new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        }
    );
    private readonly ConcurrentDictionary<string, WebSocket> _activeConnections = new();
    private int _connectionCount = 0;
    private readonly SemaphoreSlim _broadcastSemaphore = new(1, 1);

    public async Task AddWebsocketInQueueAsync(string nodeId, WebSocket socket, string? operatorId = null)
    {
        await _queue.Writer.WriteAsync((nodeId, socket, operatorId));
    }

    public async Task<(string?, WebSocket?, string?)> GetAvailableWebsocketAsync(CancellationToken ct = default)
    {
        while (await _queue.Reader.WaitToReadAsync(ct))
        {
            while (_queue.Reader.TryRead(out var item))
            {
                if (item.Item2?.State == WebSocketState.Open)
                {
                    return item;
                }
            }
        }

        return (null, null, null);
    }

    public async Task<string> AddConnectionAsync(string nodeId, WebSocket socket)
    {
        // Create unique connection ID to avoid conflicts with multiple connections from same node
        var connectionId = $"{nodeId}_{Guid.NewGuid():N}";

        _activeConnections.TryAdd(connectionId, socket);
        var newCount = Interlocked.Increment(ref _connectionCount);
        await BroadcastNodeCountAsync(newCount);

        return connectionId;
    }

    public async Task RemoveConnectionAsync(string connectionId)
    {
        if (_activeConnections.TryRemove(connectionId, out _))
        {
            var newCount = Interlocked.Decrement(ref _connectionCount);
            await BroadcastNodeCountAsync(newCount);
        }
    }

    private async Task BroadcastNodeCountAsync(int count)
    {
        await _broadcastSemaphore.WaitAsync();
        try
        {
            var message = $"node_count:{count}";
            var messageBytes = Encoding.UTF8.GetBytes(message);

            var disconnectedConnections = new List<string>();

            // Create a snapshot of connections to avoid modification during iteration
            var connectionsSnapshot = _activeConnections.ToArray();

            foreach (var kvp in connectionsSnapshot)
            {
                try
                {
                    if (kvp.Value.State == WebSocketState.Open)
                    {
                        await kvp.Value.SendAsync(
                            new ArraySegment<byte>(messageBytes),
                            WebSocketMessageType.Text,
                            true,
                            CancellationToken.None
                        );
                    }
                    else
                    {
                        disconnectedConnections.Add(kvp.Key);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WebSocketNodesQueue] Error broadcasting to {kvp.Key}: {ex.GetType().Name}");
                    disconnectedConnections.Add(kvp.Key);
                }
            }

            // Clean up disconnected connections and update count if needed
            var removedCount = 0;
            foreach (var connectionId in disconnectedConnections)
            {
                if (_activeConnections.TryRemove(connectionId, out _))
                {
                    removedCount++;
                }
            }

            // If we removed connections during cleanup, update the count
            if (removedCount > 0)
            {
                var adjustedCount = Interlocked.Add(ref _connectionCount, -removedCount);
                // Only broadcast again if the count actually changed and we have active connections
                if (adjustedCount != count && adjustedCount >= 0 && _activeConnections.Count > 0)
                {
                    var finalMessage = $"node_count:{adjustedCount}";
                    var finalMessageBytes = Encoding.UTF8.GetBytes(finalMessage);

                    foreach (var kvp in _activeConnections.ToArray())
                    {
                        try
                        {
                            if (kvp.Value.State == WebSocketState.Open)
                            {
                                await kvp.Value.SendAsync(
                                    new ArraySegment<byte>(finalMessageBytes),
                                    WebSocketMessageType.Text,
                                    true,
                                    CancellationToken.None
                                );
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[WebSocketNodesQueue] Cleanup broadcast error for {kvp.Key}: {ex.GetType().Name}");
                        }
                    }
                }
            }
        }
        finally
        {
            _broadcastSemaphore.Release();
        }
    }

    public int GetConnectionCount() => _connectionCount;
}
