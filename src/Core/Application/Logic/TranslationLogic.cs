using Domain.Contracts;
using StackExchange.Redis;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Application.Logic;

public sealed class TranslationLogic : ITranslationLogic
{
    private readonly IConnectionMultiplexer _redis;

    public TranslationLogic(IConnectionMultiplexer redis) => _redis = redis;

    public async Task ProcessTaskResponseAsync(TaskResponse taskResponse, TaskRequest taskRequest)
    {
        try
        {
            Console.WriteLine($"[TranslationLogic] Processing translation task: {taskRequest.Id}");
            
            var subscriber = _redis.GetSubscriber();
            var resultQueueName = $"result_queue_{taskRequest.Id}";
            
            // Log the data being serialized
            var responseData = taskResponse.Data;
            Console.WriteLine($"[TranslationLogic] Response data type: {responseData.GetType().FullName}");
            Console.WriteLine($"[TranslationLogic] Response object type: {responseData.Response?.GetType().FullName ?? "null"}");
            
            // Extrair apenas o objeto de resposta real (TranslationResponse)
            if (responseData.Response == null)
            {
                Console.WriteLine($"[TranslationLogic] Warning: Response object is null");
                // Enviar resposta vazia mas válida estruturalmente
                await subscriber.PublishAsync(
                    resultQueueName,
                    JsonSerializer.Serialize(new TranslationResponse {
                        TranslatedText = "",
                        SourceLanguage = "",
                        TargetLanguage = ""
                    })
                );
                return;
            }
            
            // Tentar extrair um TranslationResponse
            TranslationResponse translationResponse;
            
            try 
            {
                // Se for JsonElement ou outro tipo, tente converter
                if (responseData.Response is JsonElement jsonElement)
                {
                    // Serialize diretamente o elemento JSON
                    string serializedResponse = jsonElement.ToString();
                    await subscriber.PublishAsync(
                        resultQueueName,
                        serializedResponse
                    );
                    Console.WriteLine($"[TranslationLogic] Published JsonElement directly");
                    return;
                }
                else if (responseData.Response is TranslationResponse directResponse)
                {
                    // Já é um TranslationResponse
                    translationResponse = directResponse;
                }
                else
                {
                    // Tentar desserializar a partir do JSON
                    string jsonStr = JsonSerializer.Serialize(responseData.Response);
                    translationResponse = JsonSerializer.Deserialize<TranslationResponse>(jsonStr) 
                        ?? new TranslationResponse { TranslatedText = "", SourceLanguage = "", TargetLanguage = "" };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TranslationLogic] Error extracting TranslationResponse: {ex.Message}");
                translationResponse = new TranslationResponse {
                    TranslatedText = "",
                    SourceLanguage = "",
                    TargetLanguage = ""
                };
            }
            
            // Serializar apenas o TranslationResponse
            var serializedData = JsonSerializer.Serialize(translationResponse);
            Console.WriteLine($"[TranslationLogic] Serialized data (first 100 chars): {serializedData.Substring(0, Math.Min(100, serializedData.Length))}...");
            
            await subscriber.PublishAsync(
                resultQueueName,
                serializedData
            );
            
            Console.WriteLine($"[TranslationLogic] Successfully published translation result to {resultQueueName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TranslationLogic] Error processing translation: {ex.Message}");
            Console.WriteLine($"[TranslationLogic] Exception type: {ex.GetType().FullName}");
            Console.WriteLine($"[TranslationLogic] Stack trace: {ex.StackTrace}");
            
            // Enviar resposta de erro, mas estruturalmente válida
            try 
            {
                var subscriber = _redis.GetSubscriber();
                var resultQueueName = $"result_queue_{taskRequest.Id}";
                
                await subscriber.PublishAsync(
                    resultQueueName,
                    JsonSerializer.Serialize(new TranslationResponse {
                        TranslatedText = "",
                        SourceLanguage = "",
                        TargetLanguage = ""
                    })
                );
                
                Console.WriteLine($"[TranslationLogic] Sent error response to prevent retries");
            }
            catch (Exception innerEx)
            {
                Console.WriteLine($"[TranslationLogic] Failed to send error response: {innerEx.Message}");
            }
            
            // Não relançar a exceção
        }
    }
} 