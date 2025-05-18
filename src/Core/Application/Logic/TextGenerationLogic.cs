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
            var subscriber = _redis.GetSubscriber();
            var resultQueueName = $"result_queue_{taskRequest.Id}";

            var responseData = taskResponse.Data;

            if (responseData.Response == null)
            {
                await subscriber.PublishAsync(
                    RedisChannel.Literal(resultQueueName),
                    JsonSerializer.Serialize(new GenerationResponse { GeneratedText = "" })
                );

                return;
            }

            GenerationResponse generationResponse;

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
                else if (responseData.Response is GenerationResponse directResponse)
                {
                    generationResponse = directResponse;
                }
                else
                {
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

            var serializedData = JsonSerializer.Serialize(generationResponse);

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
        }
    }
}
