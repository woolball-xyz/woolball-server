using System.Text.Json;
using Application.Logic;
using Contracts.Constants;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace Background;

public sealed class SplitTextQueue(IServiceScopeFactory serviceScopeFactory) : BackgroundService
{
    private const int MaxChunkSize = 100; 
    
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
                Console.WriteLine($"Error in split text queue: {e.Message}");
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
            RedisChannel.Literal("split_text_queue")
        );

        consumer.OnMessage(
            async (message) =>
            {
                try
                {
                    await ProcessMessageAsync(message.Message);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error in split text queue: {e.Message}");
                }
            }
        );
    }

    private async Task ProcessMessageAsync(RedisValue message)
    {
        using var scope = serviceScopeFactory.CreateScope();
        IConnectionMultiplexer redis =
            scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
            
        var logic = scope.ServiceProvider.GetRequiredService<ITaskBusinessLogic>();

        string? messageStr = message.ToString();
        var request =
            messageStr != null
                ? System.Text.Json.JsonSerializer.Deserialize<TaskRequest>(messageStr)
                : null;
        if (request == null)
            return;

        // Extract input text, handling different possible input types
        string text = ExtractTextInput(request);
        
        if (string.IsNullOrEmpty(text))
        {
            throw new InvalidOperationException("Missing or invalid input for text processing task");
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
            request.PrivateArgs["start"] = "0";
            request.PrivateArgs["parent"] = parent;
            request.PrivateArgs["order"] = segment.Order.ToString();
            request.PrivateArgs["last"] = segment.IsLast.ToString();
            request.Id = Guid.NewGuid();
            await logic.PublishDistributeQueueAsync(request);
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
        
        // Input deve ser uma string
        if (input is string textInput)
        {
            return textInput;
        }
        
        // Se não for string, tenta converter para string
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
                IsLast = isLast
            };

            currentPosition = endIndex;
            segmentNumber++;
            
            await Task.Delay(1); // Small delay to avoid thread blocking
        }
    }
}

public class TextSegment
{
    public required string Text { get; set; }
    public required int Order { get; set; }
    public bool IsLast { get; set; }
} 