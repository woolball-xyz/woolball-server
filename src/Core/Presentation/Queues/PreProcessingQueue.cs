using System.Text.Json;
using Application.Logic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace Background;

public sealed class PreProcessingQueue(IServiceScopeFactory serviceScopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                IConnectionMultiplexer redis =
                    scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();

                var subscriber = redis.GetSubscriber();
                var consumer = await subscriber.SubscribeAsync(
                    RedisChannel.Literal("preprocessing_queue")
                );

                consumer.OnMessage(message =>
                {
                    var logic = scope.ServiceProvider.GetRequiredService<ITaskBusinessLogic>();
                    string? messageStr = message.Message.ToString();
                    TaskRequest? taskRequest =
                        messageStr != null
                            ? JsonSerializer.Deserialize<TaskRequest>(messageStr)
                            : null;
                    try
                    {
                        if (taskRequest == null)
                            return;

                        if (taskRequest.Task == "speech-to-text")
                        {
                            logic.PublishSplitAudioBySilenceQueueAsync(taskRequest);
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Error in preprocessing queue: {e.Message}");
                        if (taskRequest != null)
                        {
                            logic.EmitTaskRequestErrorAsync(taskRequest.Id.ToString());
                        }
                    }
                });

                // Keep the connection alive
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error in preprocessing queue: {e.Message}");
                // Add delay before retry
                await Task.Delay(5000, stoppingToken);
            }
        }
    }
}
