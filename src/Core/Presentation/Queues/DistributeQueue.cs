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

    public DistributeQueue(
        IServiceScopeFactory serviceScopeFactory,
        WebSocketNodesQueue webSocketNodesQueue,
        IRedisStreamPublisher publisher
    )
    {
        _serviceScopeFactory = serviceScopeFactory;
        _webSocketNodesQueue = webSocketNodesQueue;
        _publisher = publisher;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var redis = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
                var consumer = new RedisStreamConsumer(redis, StreamNames.Distribute);
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
                var (id, webSocket) =
                    await _webSocketNodesQueue.GetAvailableWebsocketAsync();

                if (id == null)
                {
                    throw new Exception("No available nodes");
                }

                taskRequest.PrivateArgs["node_id"] = id.ToString();

                using var scope = _serviceScopeFactory.CreateScope();
                var redis = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
                var db = redis.GetDatabase();

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
