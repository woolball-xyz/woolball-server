using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Presentation.Websockets;
using StackExchange.Redis;

namespace Presentation.Queues;

public sealed class DistributeQueue : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly WebSocketNodesQueue _webSocketNodesQueue;

    public DistributeQueue(
        IServiceScopeFactory serviceScopeFactory,
        WebSocketMessageSender webSocketNodesQueue
    )
    {
        _serviceScopeFactory = serviceScopeFactory;
        _webSocketNodesQueue = webSocketNodesQueue;
    }
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                IConnectionMultiplexer redis =
                    scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();

                var db = redis.GetDatabase();
                var channel = await db.SubscribeAsync("distribute_queue");

                channel.OnMessage(async message =>
                {
                    var taskRequestText = message.Message.ToString();
                    if (string.IsNullOrEmpty(taskRequestText)) return;

                    try
                    {
                        var taskRequest = JsonSerializer.Deserialize<TaskRequest>(taskRequestText);
                        if (taskRequest != null)
                        {
                            var result = await _webSocketNodesQueue.GetAvailableWebsocketAsync();
                            if (result == null)
                            {
                                Console.WriteLine("No available websockets");
                                return;
                            }
                            var (id, webSocket) = result;
                            taskRequest.PrivateArgs["node_id"] = id.ToString();

                            var encodedTask = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(taskRequest.Kwargs));
                            await webSocket.SendAsync(encodedTask, WebSocketMessageType.Text, true, stoppingToken);


                            // preserve task while it is being processed by a node
                            db.StringSet($"task:{taskRequest.Id}", taskRequestText);
                            // add task to pending tasks
                            await db.ListRightPushAsync("pending_tasks", taskRequest.Id);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error deserializing message: {ex.Message}");
                    }
                });

                // Keep the connection alive
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error in distribute queue: {e.Message}");
                // Add delay before retry
                await Task.Delay(5000, stoppingToken);
            }
        }
    }
}

public class WebSocketMessage
{
    public string TargetId { get; set; }
    public string Content { get; set; }
}
