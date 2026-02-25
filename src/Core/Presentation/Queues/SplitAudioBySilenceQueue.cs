using System.IO;
using System.Text.Json;
using Application.Logic;
using Domain.Utilities;
using Infrastructure.Redis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace Presentation.Queues;

public sealed class SplitAudioBySilenceQueue(
    IServiceScopeFactory serviceScopeFactory,
    IConnectionMultiplexer redis
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var consumer = new RedisStreamConsumer(redis, StreamNames.SplitAudioBySilence);
                await consumer.ConsumeAsync(ProcessMessageAsync, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception e)
            {
                Console.WriteLine($"Error in split audio queue: {e.Message}");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    private async Task ProcessMessageAsync(string message)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var logic = scope.ServiceProvider.GetRequiredService<ITaskBusinessLogic>();

        var request =
            !string.IsNullOrEmpty(message)
                ? JsonSerializer.Deserialize<TaskRequest>(message)
                : null;
        if (request == null)
            return;

        try
        {
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
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error in split audio queue: {e.Message}");
            await logic.EmitTaskRequestErrorAsync(request.Id.ToString());
        }
    }
}
