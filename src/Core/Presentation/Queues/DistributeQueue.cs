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
        WebSocketNodesQueue webSocketNodesQueue
    )
    {
        _serviceScopeFactory = serviceScopeFactory;
        _webSocketNodesQueue = webSocketNodesQueue;
    }

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
                var subscriber = redis.GetSubscriber();
                var channel = RedisChannel.Literal("distribute_queue");
                var subscribe = await subscriber.SubscribeAsync(channel);

                subscribe.OnMessage(async message =>
                {
                    var taskRequestText = message.Message.ToString();
                    if (string.IsNullOrEmpty(taskRequestText))
                    {
                        return;
                    }
                    try
                    {
                        var taskRequest = JsonSerializer.Deserialize<TaskRequest>(taskRequestText);
                        if (taskRequest != null)
                        {
                            var (id, webSocket) =
                                await _webSocketNodesQueue.GetAvailableWebsocketAsync();

                            if (id == null)
                            {
                                throw new Exception("No available nodes");
                            }

                            taskRequest.PrivateArgs["node_id"] = id.ToString();

                            await db.StringSetAsync($"task:{taskRequest.Id}", taskRequestText);

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

                            var subscriber = redis.GetSubscriber();

                            var channel = RedisChannel.Literal("sesion_tracking_queue");

                            await subscriber.PublishAsync(channel, taskRequest.Id.ToString());
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in distribute queue: {ex.Message}");

                        try
                        {
                            if (!string.IsNullOrEmpty(taskRequestText))
                            {
                                var taskRequest = JsonSerializer.Deserialize<TaskRequest>(
                                    taskRequestText
                                );
                                if (taskRequest != null)
                                {
                                    var logic =
                                        scope.ServiceProvider.GetRequiredService<Application.Logic.ITaskBusinessLogic>();
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
                });

                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error in distribute queue: {e.Message}");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }
}

public class WebSocketMessage
{
    public required string TargetId { get; set; }
    public required string Content { get; set; }
}
