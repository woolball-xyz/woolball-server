using System.Text.Json;
using Domain.Contracts;
using Domain.Contracts.Task.TextGeneration;
using StackExchange.Redis;

namespace Application.Logic;

public sealed class ImageTextToTextLogic : IImageTextToTextLogic
{
    private readonly IConnectionMultiplexer _redis;

    public ImageTextToTextLogic(IConnectionMultiplexer redis) => _redis = redis;

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
                    JsonSerializer.Serialize(new TextGenerationResponse { GeneratedText = "" })
                );

                return;
            }

            TextGenerationResponse generationResponse;

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
                else if (responseData.Response is TextGenerationResponse directResponse)
                {
                    generationResponse = directResponse;
                }
                else
                {
                    string jsonStr = JsonSerializer.Serialize(responseData.Response);
                    generationResponse =
                        JsonSerializer.Deserialize<TextGenerationResponse>(jsonStr)
                        ?? new TextGenerationResponse { GeneratedText = "" };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[ImageTextToTextLogic] Error extracting response: {ex.Message}"
                );
                generationResponse = new TextGenerationResponse { GeneratedText = "" };
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
                    JsonSerializer.Serialize(new TextGenerationResponse { GeneratedText = "" })
                );

                Console.WriteLine($"[ImageTextToTextLogic] Sent error response to prevent retries");
            }
            catch (Exception innerEx)
            {
                Console.WriteLine(
                    $"[ImageTextToTextLogic] Failed to send error response: {innerEx.Message}"
                );
            }
        }
    }
}
