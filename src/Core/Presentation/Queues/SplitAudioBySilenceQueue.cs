using System.IO;
using System.Text.Json;
using Application.Logic;
using Domain.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace Background;

public sealed class SplitAudioBySilenceQueue(IServiceScopeFactory serviceScopeFactory)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessQueueAsync();

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

    private async Task ProcessQueueAsync()
    {
        using var scope = serviceScopeFactory.CreateScope();
        IConnectionMultiplexer redis =
            scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();

        var subscriber = redis.GetSubscriber();

        var consumer = await subscriber.SubscribeAsync(
            RedisChannel.Literal("split_audio_by_silence_queue")
        );

        consumer.OnMessage(
            async (message) =>
            {
                try
                {
                    await ProccessMessageAsync(message.Message);
                }
                catch (Exception e)
                {
                    //emit error!!!
                    Console.WriteLine($"Error in preprocessing queue: {e.Message}");
                }
            }
        );
    }

    private async Task ProccessMessageAsync(RedisValue message)
    {
        using var scope = serviceScopeFactory.CreateScope();
        IConnectionMultiplexer redis =
            scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();

        var subscriber = redis.GetSubscriber();
        var logic = scope.ServiceProvider.GetRequiredService<ITaskBusinessLogic>();

        string? messageStr = message.ToString();
        var request =
            messageStr != null
                ? System.Text.Json.JsonSerializer.Deserialize<TaskRequest>(messageStr)
                : null;
        if (request == null)
            return;

        var filePath = request.Kwargs["input"].ToString() ?? throw new Exception("Invalid input");
        var extension = Path.GetExtension(filePath) ?? "";

        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);

        string wavFilePath = filePath;

        if (!AudioValidation.IsWav(extension))
        {
            wavFilePath = await FFmpegManager.ConvertToWavAsync(filePath);
        }

        var duration = await FFmpegManager.GetDurationAsync(wavFilePath);

        if (duration <= 0)
            throw new Exception("Invalid duration");

        if (duration < 25)
        {
            request.Kwargs["input"] = wavFilePath;
            request.PrivateArgs["start"] = "0";
            request.PrivateArgs["end"] = duration.ToString();
            request.PrivateArgs["order"] = "1";
            await logic.PublishDistributeQueueAsync(request);
            return;
        }
        var parent = request.Id.ToString();
        await foreach (var segment in FFmpegManager.BreakAudioFile(wavFilePath))
        {
            request.Kwargs["input"] = segment.FilePath;
            request.PrivateArgs["start"] = segment.StartTime.ToString();
            request.PrivateArgs["order"] = segment.Order.ToString();
            request.PrivateArgs["parent"] = parent;
            request.PrivateArgs["last"] = segment.IsLast.ToString();
            request.Id = Guid.NewGuid();
            await logic.PublishDistributeQueueAsync(request);
        }

        //update taskSession with count of segments waiting to be processed
    }
}
