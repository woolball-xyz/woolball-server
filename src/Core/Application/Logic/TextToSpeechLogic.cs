using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Domain.Contracts;
using StackExchange.Redis;

namespace Application.Logic;

public sealed class TextToSpeechLogic : ITextToSpeechLogic
{
    private readonly IConnectionMultiplexer _redis;
    private static readonly ConcurrentDictionary<string, List<TTSResponse>> _buffers = new();

    public TextToSpeechLogic(IConnectionMultiplexer redis) => _redis = redis;

    public async Task ProcessTaskResponseAsync(TaskResponse taskResponse, TaskRequest taskRequest)
    {
        try
        {
            var requestId = taskResponse.Data.RequestId;
            Console.WriteLine(
                $"[TextToSpeechLogic] Processing TTS response for request {requestId}"
            );

            bool hasParent = taskRequest.PrivateArgs.TryGetValue("parent", out var parentObj);
            string responseQueueId = hasParent && parentObj != null ? parentObj.ToString()! : taskRequest.Id.ToString();
            Console.WriteLine($"[TextToSpeechLogic] Using queue ID: {responseQueueId} (hasParent: {hasParent})");

            if (hasParent && parentObj is string parentId)
            {
                Console.WriteLine($"[TextToSpeechLogic] Processing as chunk for parent {parentId}");
                await ProcessChunkResponseAsync(taskResponse, taskRequest, parentId, responseQueueId);
            }
            else
            {
                Console.WriteLine($"[TextToSpeechLogic] Processing as single response");
                await ProcessSingleResponseAsync(taskResponse, taskRequest, responseQueueId);
            }

            Console.WriteLine(
                $"[TextToSpeechLogic] Successfully completed processing for request {requestId}"
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TextToSpeechLogic] Error processing task response: {ex.Message}");
            Console.WriteLine($"[TextToSpeechLogic] Stack trace: {ex.StackTrace}");

            try
            {
                var subscriber = _redis.GetSubscriber();
                
                bool hasParent = taskRequest.PrivateArgs.TryGetValue("parent", out var parentObj);
                string responseQueueId = hasParent && parentObj != null ? parentObj.ToString()! : taskRequest.Id.ToString();
                var resultQueueName = $"result_queue_{responseQueueId}";

                bool isChunkedResponse = hasParent;

                Console.WriteLine($"[TextToSpeechLogic] Sending error response to queue {resultQueueName}");

                if (isChunkedResponse)
                {
                    await subscriber.PublishAsync(
                        resultQueueName,
                        JsonSerializer.Serialize(new List<TTSResponse>())
                    );
                }
                else
                {
                    await subscriber.PublishAsync(
                        resultQueueName,
                        JsonSerializer.Serialize(
                            new TTSResponse
                            {
                                AudioBase64 = "",
                                Format = "wav",
                                SampleRate = 16000,
                            }
                        )
                    );
                }
                
                var completionMessage = JsonSerializer.Serialize(new { Status = "Completed" });
                await subscriber.PublishAsync(resultQueueName, completionMessage);

                Console.WriteLine(
                    $"[TextToSpeechLogic] Sent error response to prevent retries for task queue {resultQueueName}"
                );
            }
            catch (Exception innerEx)
            {
                Console.WriteLine(
                    $"[TextToSpeechLogic] Failed to send error response: {innerEx.Message}"
                );
            }
        }
    }

    private async Task ProcessSingleResponseAsync(
        TaskResponse taskResponse,
        TaskRequest taskRequest,
        string responseQueueId
    )
    {
        var subscriber = _redis.GetSubscriber();
        var resultQueueName = $"result_queue_{responseQueueId}";
        Console.WriteLine($"[TextToSpeechLogic] Single response using queue: {resultQueueName}");

        try
        {
            TTSResponse ttsResponse = ExtractTTSResponse(taskResponse.Data.Response);

            if (string.IsNullOrEmpty(ttsResponse.AudioBase64))
            {
                Console.WriteLine($"[TextToSpeechLogic] Warning: Empty audio data in response");
                ttsResponse = new TTSResponse
                {
                    AudioBase64 = "",
                    Format = "wav",
                    SampleRate = 16000,
                };
                Console.WriteLine(
                    $"[TextToSpeechLogic] Created empty but valid response to prevent retries"
                );
            }
            else
            {
                Console.WriteLine($"[TextToSpeechLogic] Audio data available in response");
            }

            string serializedResponse = JsonSerializer.Serialize(ttsResponse);
            Console.WriteLine(
                $"[TextToSpeechLogic] Serialized response (first 100 chars): {serializedResponse.Substring(0, Math.Min(100, serializedResponse.Length))}..."
            );

            await subscriber.PublishAsync(resultQueueName, serializedResponse);
            Console.WriteLine($"[TextToSpeechLogic] Published TTS response to {resultQueueName}");
            
            var completionMessage = JsonSerializer.Serialize(new { Status = "Completed" });
            await subscriber.PublishAsync(resultQueueName, completionMessage);
            Console.WriteLine($"[TextToSpeechLogic] Published completion status to {resultQueueName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[TextToSpeechLogic] Error in ProcessSingleResponseAsync: {ex.Message}"
            );
            Console.WriteLine($"[TextToSpeechLogic] Exception type: {ex.GetType().FullName}");

            try
            {
                var emptyResponse = new TTSResponse
                {
                    AudioBase64 = "",
                    Format = "wav",
                    SampleRate = 16000,
                };

                await subscriber.PublishAsync(
                    resultQueueName,
                    JsonSerializer.Serialize(emptyResponse)
                );
                
                var completionMessage = JsonSerializer.Serialize(new { Status = "Completed" });
                await subscriber.PublishAsync(resultQueueName, completionMessage);

                Console.WriteLine($"[TextToSpeechLogic] Sent error response to prevent retries");
            }
            catch (Exception innerEx)
            {
                Console.WriteLine(
                    $"[TextToSpeechLogic] Failed to send error response: {innerEx.Message}"
                );
            }
        }
    }

    private async Task ProcessChunkResponseAsync(
        TaskResponse taskResponse,
        TaskRequest taskRequest,
        string parentId,
        string responseQueueId
    )
    {
        var subscriber = _redis.GetSubscriber();
        var resultQueueName = $"result_queue_{responseQueueId}";
        Console.WriteLine($"[TextToSpeechLogic] Chunk response using queue: {resultQueueName} for parent: {parentId}");

        try
        {
            if (!_buffers.TryGetValue(parentId, out var buffer))
            {
                buffer = new List<TTSResponse>();
                _buffers[parentId] = buffer;
            }

            if (
                !taskRequest.PrivateArgs.TryGetValue("order", out var orderObj)
                || !int.TryParse(orderObj.ToString(), out int order)
            )
            {
                Console.WriteLine(
                    $"[TextToSpeechLogic] Missing order value, using default order 1"
                );
                order = 1;
            }

            bool isLast = false;
            if (
                taskRequest.PrivateArgs.TryGetValue("last", out var lastObj)
                && bool.TryParse(lastObj.ToString(), out isLast)
            )
            {
                Console.WriteLine($"[TextToSpeechLogic] Chunk {order} has isLast={isLast}");
            }
            else
            {
                isLast =
                    taskRequest.PrivateArgs.TryGetValue("retry_count", out var retryObj)
                    && int.TryParse(retryObj?.ToString() ?? "0", out var retryCount)
                    && retryCount >= 2;
                
                Console.WriteLine($"[TextToSpeechLogic] Chunk {order} calculated isLast={isLast} from retry_count");
            }

            Console.WriteLine(
                $"[TextToSpeechLogic] Processing chunk {order} (isLast: {isLast}) for parent {parentId}"
            );

            TTSResponse ttsResponse = ExtractTTSResponse(taskResponse.Data.Response);

            if (string.IsNullOrEmpty(ttsResponse.AudioBase64))
            {
                Console.WriteLine(
                    $"[TextToSpeechLogic] Warning: Empty audio data in chunk {order}"
                );
                ttsResponse = new TTSResponse
                {
                    AudioBase64 = "",
                    Format = "wav",
                    SampleRate = 16000,
                };
            }

            while (buffer.Count < order)
            {
                buffer.Add(new TTSResponse());
            }

            if (buffer.Count == order)
                buffer.Add(ttsResponse);
            else
                buffer[order - 1] = ttsResponse;

            if (isLast)
            {
                Console.WriteLine(
                    $"[TextToSpeechLogic] Last chunk received, sending complete response to {resultQueueName}"
                );

                bool hasEmptyChunk = buffer.Any(chunk =>
                    string.IsNullOrWhiteSpace(chunk.AudioBase64)
                );
                if (hasEmptyChunk)
                {
                    Console.WriteLine(
                        $"[TextToSpeechLogic] Warning: Some chunks have empty audio data"
                    );
                }

                string serializedResponse = JsonSerializer.Serialize(buffer);
                Console.WriteLine(
                    $"[TextToSpeechLogic] Serialized complete response (first 100 chars): {serializedResponse.Substring(0, Math.Min(100, serializedResponse.Length))}..."
                );

                await subscriber.PublishAsync(resultQueueName, serializedResponse);
                
                var completionMessage = JsonSerializer.Serialize(new { Status = "Completed" });
                await subscriber.PublishAsync(resultQueueName, completionMessage);
                Console.WriteLine($"[TextToSpeechLogic] Published completion status to {resultQueueName}");

                _buffers.TryRemove(parentId, out _);

                Console.WriteLine(
                    $"[TextToSpeechLogic] Successfully published final chunk response to {resultQueueName}"
                );
            }
            else
            {
                Console.WriteLine(
                    $"[TextToSpeechLogic] Chunk {order} processed and stored, waiting for more chunks"
                );
                
                var currentRequestQueueName = $"result_queue_{taskRequest.Id}";
                if (currentRequestQueueName != resultQueueName)
                {
                    Console.WriteLine($"[TextToSpeechLogic] Sending completion status to chunk queue {currentRequestQueueName}");
                    var chunkCompletionMessage = JsonSerializer.Serialize(new { Status = "Completed" });
                    await subscriber.PublishAsync(currentRequestQueueName, chunkCompletionMessage);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[TextToSpeechLogic] Error in ProcessChunkResponseAsync: {ex.Message}"
            );
            Console.WriteLine($"[TextToSpeechLogic] Exception type: {ex.GetType().FullName}");

            try
            {
                var emptyResponseList = new List<TTSResponse>
                {
                    new TTSResponse
                    {
                        AudioBase64 = "",
                        Format = "wav",
                        SampleRate = 16000,
                    },
                };

                await subscriber.PublishAsync(
                    resultQueueName,
                    JsonSerializer.Serialize(emptyResponseList)
                );
                
                var completionMessage = JsonSerializer.Serialize(new { Status = "Completed" });
                await subscriber.PublishAsync(resultQueueName, completionMessage);

                _buffers.TryRemove(parentId, out _);

                Console.WriteLine(
                    $"[TextToSpeechLogic] Sent error response to prevent retries for parent {parentId}"
                );
            }
            catch (Exception innerEx)
            {
                Console.WriteLine(
                    $"[TextToSpeechLogic] Failed to send error response: {innerEx.Message}"
                );
            }
        }
    }

    private TTSResponse ExtractTTSResponse(object responseObj)
    {
        try
        {
            Console.WriteLine(
                $"[TextToSpeechLogic] ExtractTTSResponse from type: {responseObj?.GetType().FullName ?? "null"}"
            );

            if (responseObj is TTSResponse ttsResponse)
            {
                Console.WriteLine("[TextToSpeechLogic] Response is already TTSResponse");
                return ttsResponse;
            }

            if (responseObj is JsonElement jsonElement)
            {
                Console.WriteLine("[TextToSpeechLogic] Response is JsonElement");

                if (jsonElement.TryGetProperty("audio", out var audioProperty))
                {
                    return new TTSResponse
                    {
                        AudioBase64 = audioProperty.GetString() ?? string.Empty,
                        Format = jsonElement.TryGetProperty("format", out var formatProp)
                            ? formatProp.GetString() ?? "wav"
                            : "wav",
                        SampleRate = jsonElement.TryGetProperty(
                            "sample_rate",
                            out var sampleRateProp
                        )
                            ? sampleRateProp.GetInt32()
                            : 16000,
                    };
                }

                if (jsonElement.TryGetProperty("response", out var responseProp))
                {
                    if (
                        responseProp.ValueKind == JsonValueKind.Object
                        && responseProp.TryGetProperty("audio", out var innerAudioProp)
                    )
                    {
                        return new TTSResponse
                        {
                            AudioBase64 = innerAudioProp.GetString() ?? string.Empty,
                            Format = responseProp.TryGetProperty("format", out var formatProp)
                                ? formatProp.GetString() ?? "wav"
                                : "wav",
                            SampleRate = responseProp.TryGetProperty(
                                "sample_rate",
                                out var sampleRateProp
                            )
                                ? sampleRateProp.GetInt32()
                                : 16000,
                        };
                    }
                }
            }

            string json = JsonSerializer.Serialize(responseObj);
            Console.WriteLine(
                $"[TextToSpeechLogic] Attempting to extract from JSON: {json.Substring(0, Math.Min(100, json.Length))}..."
            );

            try
            {
                var result = JsonSerializer.Deserialize<TTSResponse>(json);
                if (result != null && !string.IsNullOrEmpty(result.AudioBase64))
                {
                    Console.WriteLine(
                        "[TextToSpeechLogic] Successfully extracted TTSResponse from JSON"
                    );
                    return result;
                }

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string audioBase64 = FindAudioProperty(root);
                if (!string.IsNullOrEmpty(audioBase64))
                {
                    Console.WriteLine("[TextToSpeechLogic] Found audio property in JSON");
                    return new TTSResponse
                    {
                        AudioBase64 = audioBase64,
                        Format = "wav",
                        SampleRate = 16000,
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TextToSpeechLogic] Error parsing JSON: {ex.Message}");
            }

            Console.WriteLine($"[TextToSpeechLogic] Could not extract TTSResponse");
            return new TTSResponse();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TextToSpeechLogic] Error extracting TTSResponse: {ex.Message}");
            return new TTSResponse();
        }
    }

    private string FindAudioProperty(JsonElement element, int depth = 0)
    {
        if (depth > 3)
            return string.Empty;

        if (
            element.TryGetProperty("audio", out var audioProp)
            && audioProp.ValueKind == JsonValueKind.String
        )
        {
            return audioProp.GetString() ?? string.Empty;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                if (
                    prop.Value.ValueKind == JsonValueKind.Object
                    || prop.Value.ValueKind == JsonValueKind.Array
                )
                {
                    string result = FindAudioProperty(prop.Value, depth + 1);
                    if (!string.IsNullOrEmpty(result))
                    {
                        return result;
                    }
                }
            }
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                string result = FindAudioProperty(item, depth + 1);
                if (!string.IsNullOrEmpty(result))
                {
                    return result;
                }
            }
        }

        return string.Empty;
    }
}
