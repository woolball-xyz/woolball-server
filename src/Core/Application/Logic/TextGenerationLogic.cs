using System.Text.Json;
using System.Text.Json.Nodes;
using Domain.Contracts;
using StackExchange.Redis;

namespace Application.Logic;

public sealed class TextGenerationLogic : ITextGenerationLogic
{
    private readonly IConnectionMultiplexer _redis;

    public TextGenerationLogic(IConnectionMultiplexer redis) => _redis = redis;

    public async Task ProcessTaskResponseAsync(TaskResponse taskResponse, TaskRequest taskRequest)
    {
        try
        {
            Console.WriteLine(
                $"[TextGenerationLogic] Processing text generation task: {taskRequest.Id}"
            );

            var subscriber = _redis.GetSubscriber();
            var resultQueueName = $"result_queue_{taskRequest.Id}";

            // Log the data being serialized
            var responseData = taskResponse.Data;
            Console.WriteLine(
                $"[TextGenerationLogic] Response data type: {responseData.GetType().FullName}"
            );
            Console.WriteLine(
                $"[TextGenerationLogic] Response object type: {responseData.Response?.GetType().FullName ?? "null"}"
            );

            // Verificar se temos um objeto de resposta utilizável
            if (responseData.Response == null)
            {
                Console.WriteLine($"[TextGenerationLogic] Warning: Response object is null");

                // Enviar uma resposta genérica para evitar falhas - apenas o GenerationResponse
                await subscriber.PublishAsync(
                    resultQueueName,
                    JsonSerializer.Serialize(new GenerationResponse { GeneratedText = "" })
                );

                return;
            }

            // Tentar extrair um GenerationResponse
            GenerationResponse generationResponse;

            try
            {
                // Se for JsonElement ou outro tipo, tente converter
                if (responseData.Response is JsonElement jsonElement)
                {
                    // Serialize diretamente o elemento JSON
                    string serializedResponse = jsonElement.ToString();
                    await subscriber.PublishAsync(resultQueueName, serializedResponse);
                    Console.WriteLine($"[TextGenerationLogic] Published JsonElement directly");
                    return;
                }
                else if (responseData.Response is GenerationResponse directResponse)
                {
                    // Já é um GenerationResponse
                    generationResponse = directResponse;
                }
                else
                {
                    // Tentar desserializar a partir do JSON
                    string jsonStr = JsonSerializer.Serialize(responseData.Response);
                    generationResponse =
                        JsonSerializer.Deserialize<GenerationResponse>(jsonStr)
                        ?? new GenerationResponse { GeneratedText = "" };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[TextGenerationLogic] Error extracting GenerationResponse: {ex.Message}"
                );
                generationResponse = new GenerationResponse { GeneratedText = "" };
            }

            // Serializar apenas o GenerationResponse
            var serializedData = JsonSerializer.Serialize(generationResponse);
            Console.WriteLine(
                $"[TextGenerationLogic] Serialized data (first 100 chars): {serializedData.Substring(0, Math.Min(100, serializedData.Length))}..."
            );

            await subscriber.PublishAsync(resultQueueName, serializedData);

            Console.WriteLine(
                $"[TextGenerationLogic] Successfully published text generation result to {resultQueueName}"
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[TextGenerationLogic] Error processing text generation: {ex.Message}"
            );
            Console.WriteLine($"[TextGenerationLogic] Exception type: {ex.GetType().FullName}");
            Console.WriteLine($"[TextGenerationLogic] Stack trace: {ex.StackTrace}");

            try
            {
                // Tenta enviar uma resposta de erro para evitar retentativas - apenas o GenerationResponse
                var subscriber = _redis.GetSubscriber();
                var resultQueueName = $"result_queue_{taskRequest.Id}";

                await subscriber.PublishAsync(
                    resultQueueName,
                    JsonSerializer.Serialize(new GenerationResponse { GeneratedText = "" })
                );

                Console.WriteLine($"[TextGenerationLogic] Sent error response to prevent retries");
            }
            catch (Exception innerEx)
            {
                Console.WriteLine(
                    $"[TextGenerationLogic] Failed to send error response: {innerEx.Message}"
                );
            }

            // Não reenviar a exceção para evitar retentativas
        }
    }
}
