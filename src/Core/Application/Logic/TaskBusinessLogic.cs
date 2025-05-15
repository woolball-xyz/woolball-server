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
            Console.WriteLine($"Erro ao publicar na fila de divisão de texto: {ex.Message}");
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
            Console.WriteLine($"Erro ao publicar na fila de distribuição: {ex.Message}");
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
        var subscriber = redis.GetSubscriber();
        var queueName = $"result_queue_{taskRequest.Id}";

        // Comportamento para requisições streaming
        var channel = await subscriber.SubscribeAsync(RedisChannel.Literal(queueName));

        Console.WriteLine($"[StreamTaskResultAsync] listening: {queueName}");
        while (!cancellationToken.IsCancellationRequested)
        {
            var message = await channel.ReadAsync();
            if (message.Message.IsNullOrEmpty)
                continue;

            string messageText = message.Message.ToString();
            Console.WriteLine($"[StreamTaskResultAsync] Mensagem recebida: {messageText}");

            // Verifica se é uma mensagem de conclusão
            if (
                messageText.Contains("\"Status\":\"Completed\"", StringComparison.OrdinalIgnoreCase)
            )
            {
                Console.WriteLine(
                    $"[StreamTaskResultAsync] Detected completion message, breaking stream"
                );
                break; // Não enviamos a mensagem de status para o cliente, apenas encerramos o stream
            }

            yield return messageText;
        }

        await channel.UnsubscribeAsync();
    }
}
