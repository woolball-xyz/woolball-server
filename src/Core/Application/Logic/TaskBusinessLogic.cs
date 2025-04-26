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
                Error = "Falha no processamento após múltiplas tentativas",
            };
            await subscriber.PublishAsync(
                queueName,
                System.Text.Json.JsonSerializer.Serialize(message)
            );
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao emitir erro para a tarefa {taskRequestId}: {ex.Message}");
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
            var channel = await subscriber.SubscribeAsync(RedisChannel.Literal(queueName));

            Console.WriteLine($"[AwaitTaskResultAsync] listening: {queueName}");
            var result = await channel.ReadAsync();
            Console.WriteLine($"[AwaitTaskResultAsync] Mensagem recebida: {result.Message}");
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

            // Verificar se já existe contagem de tentativas para streaming
            if (!taskRequest.PrivateArgs.ContainsKey("stream_retry_count"))
            {
                taskRequest.PrivateArgs["stream_retry_count"] = 0;
            }

            channel = await subscriber.SubscribeAsync(queueName);
            tcs = new TaskCompletionSource<bool>();
            messageQueue = new System.Collections.Concurrent.ConcurrentQueue<string>();
            messageReceived = new System.Threading.AutoResetEvent(false);

            // Single combined message handler
            channel.OnMessage(async message =>
            {
                var messageContent = message.Message.ToString();

                // Verificar se a mensagem contém um erro
                if (
                    messageContent.Contains("\"Status\":\"Error\"")
                    || messageContent.Contains("\"error\":")
                )
                {
                    Console.WriteLine($"Erro detectado durante streaming: {messageContent}");
                    messageQueue.Enqueue(messageContent);
                    messageReceived.Set();
                    tcs.TrySetResult(true); // Finalizar o streaming quando ocorrer um erro
                }
                else if (messageContent.Contains("\"Status\":\"Completed\""))
                {
                    await channel.UnsubscribeAsync();
                    tcs.TrySetResult(true);
                    return;
                }

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
