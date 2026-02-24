using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Infrastructure.Redis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Presentation.Websockets;
using StackExchange.Redis;

namespace Presentation.Queues;

public sealed class DistributeQueue : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly WebSocketNodesQueue _webSocketNodesQueue;
    private readonly IRedisStreamPublisher _publisher;
    private readonly IConnectionMultiplexer _redis;

    public DistributeQueue(
        IServiceScopeFactory serviceScopeFactory,
        WebSocketNodesQueue webSocketNodesQueue,
        IRedisStreamPublisher publisher,
        IConnectionMultiplexer redis
    )
    {
        _serviceScopeFactory = serviceScopeFactory;
        _webSocketNodesQueue = webSocketNodesQueue;
        _publisher = publisher;
        _redis = redis;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var consumer = new RedisStreamConsumer(_redis, StreamNames.Distribute);
                await consumer.ConsumeAsync(msg => ProcessMessageAsync(msg, stoppingToken), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception e)
            {
                Console.WriteLine($"Error in distribute queue: {e.Message}");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    private async Task ProcessMessageAsync(string message, CancellationToken stoppingToken)
    {
        if (string.IsNullOrEmpty(message))
            return;

        try
        {
            var taskRequest = JsonSerializer.Deserialize<TaskRequest>(message);
            if (taskRequest != null)
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));

                string id;
                System.Net.WebSockets.WebSocket webSocket;
                try
                {
                    (id, webSocket) = await _webSocketNodesQueue.GetAvailableWebsocketAsync(timeoutCts.Token);
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                {
                    throw new Exception("No available nodes (timeout after 30s)");
                }

                if (id == null)
                {
                    throw new Exception("No available nodes");
                }

                taskRequest.PrivateArgs["node_id"] = id.ToString();

                var db = _redis.GetDatabase();

                await db.StringSetAsync($"task:{taskRequest.Id}", message, TimeSpan.FromMinutes(10));

                await taskRequest.LoadInputIfNeeded();

                var encodedTask = Encoding.UTF8.GetBytes(
                    JsonSerializer.Serialize(
                        new
                        {
                            Id = taskRequest.Id,
                            Key = taskRequest.Task,
                            Value = taskRequest.Kwargs,
                        }
                    )
                );

                await webSocket.SendAsync(
                    encodedTask,
                    WebSocketMessageType.Text,
                    true,
                    stoppingToken
                );

                await _publisher.PublishAsync(StreamNames.SessionTracking, taskRequest.Id.ToString());
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in distribute queue: {ex.Message}");

            try
            {
                if (!string.IsNullOrEmpty(message))
                {
                    var taskRequest = JsonSerializer.Deserialize<TaskRequest>(message);
                    if (taskRequest != null)
                    {
                        using var errorScope = _serviceScopeFactory.CreateScope();
                        var logic =
                            errorScope.ServiceProvider.GetRequiredService<Application.Logic.ITaskBusinessLogic>();
                        await logic.EmitTaskRequestErrorAsync(
                            taskRequest.Id.ToString()
                        );
                        Console.WriteLine($"Error emitted for task {taskRequest.Id}");
                    }
                }
            }
            catch (Exception innerEx)
            {
                Console.WriteLine($"Failed to emit error: {innerEx.Message}");
            }
        }
    }
}
