using Domain.Contracts;
using StackExchange.Redis;

namespace Application.Logic;

public sealed class TaskBusinessLogic(IConnectionMultiplexer redis) : ITaskBusinessLogic
{
    public async Task<bool> EmitTaskRequestErrorAsync(string taskRequestId)
    {
        try
        {
            var subscriber = redis.GetSubscriber();
            var queueName = $"result_queue_{taskRequestId}";
            var message = new
            {
                taskRequestId,
                Status = "Error",
                Error = "Processing failed after multiple attempts",
            };
            await subscriber.PublishAsync(
                queueName,
                System.Text.Json.JsonSerializer.Serialize(message)
            );
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error emitting error for task: {ex.GetType().Name}");
            return false;
        }
    }

    public async Task<bool> PublishPreProcessingQueueAsync(TaskRequest taskRequest)
    {
        try
        {
            Console.WriteLine("Publishing preprocessing queue...");
            var subscriber = redis.GetSubscriber();
            var queueName = RedisChannel.Literal("preprocessing_queue");
            await subscriber.PublishAsync(
                queueName,
                System.Text.Json.JsonSerializer.Serialize(taskRequest)
            );
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TaskBusinessLogic] Error publishing to preprocessing queue: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> PublishSplitAudioBySilenceQueueAsync(TaskRequest taskRequest)
    {
        try
        {
            var subscriber = redis.GetSubscriber();
            var queueName = RedisChannel.Literal("split_audio_by_silence_queue");
            await subscriber.PublishAsync(
                queueName,
                System.Text.Json.JsonSerializer.Serialize(taskRequest)
            );
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TaskBusinessLogic] Error publishing to split audio queue: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> PublishSplitTextQueueAsync(TaskRequest taskRequest)
    {
        try
        {
            var subscriber = redis.GetSubscriber();
            var queueName = RedisChannel.Literal("split_text_queue");
            await subscriber.PublishAsync(
                queueName,
                System.Text.Json.JsonSerializer.Serialize(taskRequest)
            );
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error publishing to text split queue: {ex.GetType().Name}");
            return false;
        }
    }

    public async Task<bool> PublishDistributeQueueAsync(TaskRequest taskRequest)
    {
        try
        {
            var subscriber = redis.GetSubscriber();
            var queueName = RedisChannel.Literal("distribute_queue");
            await subscriber.PublishAsync(
                queueName,
                System.Text.Json.JsonSerializer.Serialize(taskRequest)
            );
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error publishing to distribution queue: {ex.GetType().Name}");
            return false;
        }
    }

    public async Task<string> AwaitTaskResultAsync(TaskRequest taskRequest)
    {
        try
        {
            var subscriber = redis.GetSubscriber();
            var queueName = $"result_queue_{taskRequest.Id}";

            var channel = await subscriber.SubscribeAsync(RedisChannel.Literal(queueName));

            var result = await channel.ReadAsync();
            await channel.UnsubscribeAsync();
            return result.Message.ToString();
        }
        catch (Exception ex)
        {
            throw new Exception($"Error waiting for task result: {ex.Message}", ex);
        }
    }

    public async IAsyncEnumerable<string> StreamTaskResultAsync(
        TaskRequest taskRequest,
        CancellationToken cancellationToken = default
    )
    {
        var subscriber = redis.GetSubscriber();
        var queueName = $"result_queue_{taskRequest.Id}";

        var channel = await subscriber.SubscribeAsync(RedisChannel.Literal(queueName));

        while (!cancellationToken.IsCancellationRequested)
        {
            var message = await channel.ReadAsync(cancellationToken);
            if (message.Message.IsNullOrEmpty)
                continue;

            string messageText = message.Message.ToString();

            if (
                messageText.Contains("\"Status\":\"Completed\"", StringComparison.OrdinalIgnoreCase)
            )
            {
                break;
            }

            yield return messageText;
        }

        await channel.UnsubscribeAsync();
    }
}
