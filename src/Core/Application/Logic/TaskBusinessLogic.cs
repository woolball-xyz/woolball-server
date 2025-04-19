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
            var message = new { taskRequestId, Status = "Error" };
            await subscriber.PublishAsync(
                queueName,
                System.Text.Json.JsonSerializer.Serialize(message)
            );
            return true;
        }
        catch (Exception ex)
        {
            return false;
        }
    }

    public async Task<bool> PublishPreProcessingQueueAsync(TaskRequest taskRequest)
    {
        try
        {
            var subscriber = redis.GetSubscriber();
            var queueName = "preprocessing_queue";
            await subscriber.PublishAsync(
                queueName,
                System.Text.Json.JsonSerializer.Serialize(taskRequest)
            );
            return true;
        }
        catch (Exception ex)
        {
            return false;
        }
    }

    public async Task<bool> PublishSplitAudioBySilenceQueueAsync(TaskRequest taskRequest)
    {
        try
        {
            var subscriber = redis.GetSubscriber();
            var queueName = "split_audio_by_silence_queue";
            await subscriber.PublishAsync(
                queueName,
                System.Text.Json.JsonSerializer.Serialize(taskRequest)
            );
            return true;
        }
        catch (Exception ex)
        {
            return false;
        }
    }

    public async Task<string> AwaitTaskResultAsync(TaskRequest taskRequest)
    {
        try
        {
            var subscriber = redis.GetSubscriber();
            var queueName = $"result_queue_{taskRequest.Id}";

            // Comportamento para requisições não-streaming
            var channel = await subscriber.SubscribeAsync(queueName);
            var result = await channel.ReadAsync();
            await channel.UnsubscribeAsync();
            return result.Message.ToString();
        }
        catch (Exception ex)
        {
            throw new Exception($"Error waiting for task result: {ex.Message}");
        }
    }

    public async IAsyncEnumerable<string> StreamTaskResultAsync(
        TaskRequest taskRequest,
        CancellationToken cancellationToken = default
    )
    {
        ChannelMessageQueue channel = null;
        System.Collections.Concurrent.ConcurrentQueue<string> messageQueue = null;
        System.Threading.AutoResetEvent messageReceived = null;
        TaskCompletionSource<bool> tcs = null;

        try
        {
            var subscriber = redis.GetSubscriber();
            var queueName = $"result_queue_{taskRequest.Id}";

            channel = await subscriber.SubscribeAsync(queueName);
            tcs = new TaskCompletionSource<bool>();
            messageQueue = new System.Collections.Concurrent.ConcurrentQueue<string>();
            messageReceived = new System.Threading.AutoResetEvent(false);

            // Configurar handlers para mensagens
            channel.OnMessage(async message =>
            {
                var messageContent = message.Message.ToString();

                if (
                    messageContent.Contains("\"Status\":\"Completed\"")
                    || messageContent.Contains("\"Status\":\"Error\"")
                )
                {
                    await channel.UnsubscribeAsync();
                    tcs.TrySetResult(true);
                }
            });

            channel.OnMessage(message =>
            {
                var messageContent = message.Message.ToString();
                messageQueue.Enqueue(messageContent);
                messageReceived.Set();
            });

            cancellationToken.Register(() =>
            {
                tcs.TrySetCanceled();
                messageReceived.Set();
            });
        }
        catch (Exception ex)
        {
            if (channel != null)
            {
                await channel.UnsubscribeAsync();
            }
            throw new Exception($"Error setting up streaming task result: {ex.Message}");
        }

        // Processamento de mensagens fora do bloco try-catch
        while (!tcs.Task.IsCompleted && !cancellationToken.IsCancellationRequested)
        {
            if (messageQueue.TryDequeue(out string message))
            {
                yield return message;
            }
            else
            {
                try
                {
                    await Task.Run(() => messageReceived.WaitOne(1000), cancellationToken);
                }
                catch (Exception ex)
                {
                    if (channel != null)
                    {
                        await channel.UnsubscribeAsync();
                    }
                    throw new Exception($"Error waiting for messages: {ex.Message}");
                }
            }
        }

        // Processar mensagens restantes na fila
        while (messageQueue.TryDequeue(out string message))
        {
            yield return message;
        }

        // Garantir que o canal seja fechado
        if (channel != null)
        {
            try
            {
                await channel.UnsubscribeAsync();
            }
            catch
            {
                // Ignorar erros ao fechar o canal
            }
        }
    }
}
