using System.Text.Json;
using Application.Logic;
using Contracts.Constants;
using Infrastructure.Redis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace Presentation.Queues;

public sealed class SplitTextQueue(
    IServiceScopeFactory serviceScopeFactory,
    IConnectionMultiplexer redis
) : BackgroundService
{
    private const int MaxChunkSize = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var consumer = new RedisStreamConsumer(redis, StreamNames.SplitText);
                await consumer.ConsumeAsync(ProcessMessageAsync, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception e)
            {
                Console.WriteLine($"Error in split text queue: {e.Message}");
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
            // Extract input text, handling different possible input types
            string text = ExtractTextInput(request);

            if (string.IsNullOrEmpty(text))
            {
                throw new InvalidOperationException(
                    "Missing or invalid input for text processing task"
                );
            }

            if (text.Length <= MaxChunkSize)
            {
                await logic.PublishDistributeQueueAsync(request);
                return;
            }

            var parent = request.Id.ToString();
            await foreach (var segment in BreakTextIntoChunks(text))
            {
                request.Kwargs["input"] = segment.Text;
                request.PrivateArgs["parent"] = parent;
                request.PrivateArgs["order"] = segment.Order.ToString();
                request.PrivateArgs["last"] = segment.IsLast.ToString();
                request.Id = Guid.NewGuid();
                await logic.PublishDistributeQueueAsync(request);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error in split text queue: {e.Message}");
            await logic.EmitTaskRequestErrorAsync(request.Id.ToString());
        }
    }

    // Helper method to extract text input from different possible formats
    private string ExtractTextInput(TaskRequest request)
    {
        if (!request.Kwargs.ContainsKey("input"))
        {
            return string.Empty;
        }

        var input = request.Kwargs["input"];

        if (input is string textInput)
        {
            return textInput;
        }

        return input?.ToString() ?? string.Empty;
    }

    private async IAsyncEnumerable<TextSegment> BreakTextIntoChunks(string text)
    {
        var sentenceEnders = new[] { '.', '!', '?', '\n' };
        var currentPosition = 0;
        var segmentNumber = 1;

        while (currentPosition < text.Length)
        {
            int endIndex;

            if (currentPosition + MaxChunkSize >= text.Length)
            {
                endIndex = text.Length;
            }
            else
            {
                endIndex = currentPosition + MaxChunkSize;

                while (endIndex > currentPosition && !sentenceEnders.Contains(text[endIndex - 1]))
                {
                    endIndex--;
                }

                if (endIndex <= currentPosition)
                {
                    endIndex = Math.Min(currentPosition + MaxChunkSize, text.Length);
                }
            }

            var segmentText = text.Substring(currentPosition, endIndex - currentPosition).Trim();
            bool isLast = endIndex >= text.Length;

            yield return new TextSegment
            {
                Text = segmentText,
                Order = segmentNumber,
                IsLast = isLast,
            };

            currentPosition = endIndex;
            segmentNumber++;
        }
    }
}

public class TextSegment
{
    public required string Text { get; set; }
    public required int Order { get; set; }
    public bool IsLast { get; set; }
}
