using Application.Logic;
using Domain.WebServices;
using Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
                var db = redis.GetDatabase();
                var channel = await db.SubscribeAsync("preprocessing_queue");

                channel.OnMessage(message =>
                {
                    var logic = scope.ServiceProvider.GetRequiredService<ITaskBusinessLogic>();
                    TaskRequest taskRequest = JsonSerializer.Deserialize<TaskRequest>(
                        message.Message.ToString()
                    );
                    try
                    {
                        if (taskRequest == null)
                            return;

                        if (taskRequest.Task == "speech-to-text")
                        {
                            logic.PublishSplitAudioBySilenceQueueAsync(taskRequest.Id, taskRequest);
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Error in preprocessing queue: {e.Message}");
                        logic.EmitTaskRequestErrorAsync(taskRequest.Id);
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
