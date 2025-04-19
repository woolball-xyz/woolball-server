using System.Text.Json;
using Application.Logic;
using Contracts.Constants;
using Domain.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace Background;

public sealed class PostProcessingQueue(IServiceScopeFactory serviceScopeFactory)
    : BackgroundService
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

                var db = redis.GetDatabase();
                var subscriber = redis.GetSubscriber();
                var consumer = await subscriber.SubscribeAsync(
                    RedisChannel.Literal("post_processing_queue")
                );

                consumer.OnMessage(async message =>
                {
                    string? taskId = null;
                    try
                    {
                        var messageStr = message.Message.ToString();

                        if (string.IsNullOrEmpty(messageStr))
                            return;

                        var taskResponse = JsonSerializer.Deserialize<TaskResponse>(messageStr);
                        if (taskResponse == null)
                            return;

                        var request = await db.StringGetAsync(
                            $"task:{taskResponse.Data.RequestId}"
                        );

                        if (!request.HasValue)
                        {
                            //emit redistribute
                        }

                        var taskRequest = JsonSerializer.Deserialize<TaskRequest>(
                            request.ToString()
                        );
                        if (taskRequest == null)
                        {
                            //emit redistribute
                        }
                        taskId = taskRequest.Id.ToString();
                        await ProcessTaskResponseAsync(taskResponse, taskRequest);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Error in postprocessing queue: {e.Message}");
                        if (taskId != null)
                        {
                            var logic =
                                scope.ServiceProvider.GetRequiredService<ITaskBusinessLogic>();
                            logic.EmitTaskRequestErrorAsync(taskId);
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

    private async Task ProcessTaskResponseAsync(TaskResponse taskResponse, TaskRequest taskRequest)
    {
        using var scope = serviceScopeFactory.CreateScope();

        switch (taskRequest.Task)
        {
            case "speech-recognition":
                var speechToTextLogic =
                    scope.ServiceProvider.GetRequiredService<SpeechToTextLogic>();
                await speechToTextLogic.ProcessTaskResponseAsync(taskResponse, taskRequest);
                break;

            default:
                break;
        }
    }
}
