using Domain.Contracts;
using Infrastructure.Redis;
using StackExchange.Redis;

namespace Application.Logic;

public sealed class TaskBusinessLogic(IConnectionMultiplexer redis, IRedisStreamPublisher streamPublisher) : ITaskBusinessLogic
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
            await streamPublisher.PublishAsync(
                StreamNames.PreProcessing,
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
            await streamPublisher.PublishAsync(
                StreamNames.SplitAudioBySilence,
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
            await streamPublisher.PublishAsync(
                StreamNames.SplitText,
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
            await streamPublisher.PublishAsync(
                StreamNames.Distribute,
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

    public async Task<string> AwaitTaskResultAsync(TaskRequest taskRequest, CancellationToken cancellationToken = default)
    {
        // 3-minute safety-net timeout (> 2-minute session tracking timeout)
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromMinutes(3));

        var subscriber = redis.GetSubscriber();
        var queueName = $"result_queue_{taskRequest.Id}";

        var channel = await subscriber.SubscribeAsync(RedisChannel.Literal(queueName));

        try
        {
            var result = await channel.ReadAsync(timeoutCts.Token);
            return result.Message.ToString();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Task {taskRequest.Id} timed out waiting for result");
        }
        finally
        {
            await channel.UnsubscribeAsync();
        }
    }

    public async IAsyncEnumerable<string> StreamTaskResultAsync(
        TaskRequest taskRequest,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default
    )
    {
        var subscriber = redis.GetSubscriber();
        var queueName = $"result_queue_{taskRequest.Id}";

        var channel = await subscriber.SubscribeAsync(RedisChannel.Literal(queueName));

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var message = await channel.ReadAsync(cancellationToken);
                if (message.Message.IsNullOrEmpty)
                    continue;

                string messageText = message.Message.ToString();

                if (messageText.Contains("\"Status\":\"Completed\"", StringComparison.OrdinalIgnoreCase))
                    break;

                if (messageText.Contains("\"Status\":\"Error\"", StringComparison.OrdinalIgnoreCase))
                {
                    yield return messageText;
                    break;
                }

                yield return messageText;
            }
        }
        finally
        {
            await channel.UnsubscribeAsync();
        }
    }
}
