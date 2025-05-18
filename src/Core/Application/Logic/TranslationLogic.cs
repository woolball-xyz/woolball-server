using System.Text.Json;
using System.Text.Json.Nodes;
using Domain.Contracts;
using StackExchange.Redis;

namespace Application.Logic;

public sealed class TranslationLogic : ITranslationLogic
{
    private readonly IConnectionMultiplexer _redis;

    public TranslationLogic(IConnectionMultiplexer redis) => _redis = redis;

    public async Task ProcessTaskResponseAsync(TaskResponse taskResponse, TaskRequest taskRequest)
    {
        try
        {
            var subscriber = _redis.GetSubscriber();
            var resultQueueName = $"result_queue_{taskRequest.Id}";

            var responseData = taskResponse.Data;

            if (responseData.Response == null)
            {
                await subscriber.PublishAsync(
                    RedisChannel.Literal(resultQueueName),
                    JsonSerializer.Serialize(
                        new TranslationResponse
                        {
                            TranslatedText = "",
                            SourceLanguage = "",
                            TargetLanguage = "",
                        }
                    )
                );
                return;
            }

            TranslationResponse translationResponse;

            try
            {
                if (responseData.Response is JsonElement jsonElement)
                {
                    string serializedResponse = jsonElement.ToString();
                    await subscriber.PublishAsync(
                        RedisChannel.Literal(resultQueueName),
                        serializedResponse
                    );
                    return;
                }
                else if (responseData.Response is TranslationResponse directResponse)
                {
                    translationResponse = directResponse;
                }
                else
                {
                    string jsonStr = JsonSerializer.Serialize(responseData.Response);
                    translationResponse =
                        JsonSerializer.Deserialize<TranslationResponse>(jsonStr)
                        ?? new TranslationResponse
                        {
                            TranslatedText = "",
                            SourceLanguage = "",
                            TargetLanguage = "",
                        };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[TranslationLogic] Error extracting TranslationResponse: {ex.Message}"
                );
                translationResponse = new TranslationResponse
                {
                    TranslatedText = "",
                    SourceLanguage = "",
                    TargetLanguage = "",
                };
            }

            var serializedData = JsonSerializer.Serialize(translationResponse);

            await subscriber.PublishAsync(RedisChannel.Literal(resultQueueName), serializedData);
        }
        catch (Exception ex)
        {
            try
            {
                var subscriber = _redis.GetSubscriber();
                var resultQueueName = $"result_queue_{taskRequest.Id}";

                await subscriber.PublishAsync(
                    RedisChannel.Literal(resultQueueName),
                    JsonSerializer.Serialize(
                        new TranslationResponse
                        {
                            TranslatedText = "",
                            SourceLanguage = "",
                            TargetLanguage = "",
                        }
                    )
                );

                Console.WriteLine($"[TranslationLogic] Sent error response to prevent retries");
            }
            catch (Exception innerEx)
            {
                Console.WriteLine(
                    $"[TranslationLogic] Failed to send error response: {innerEx.Message}"
                );
            }
        }
    }
}
