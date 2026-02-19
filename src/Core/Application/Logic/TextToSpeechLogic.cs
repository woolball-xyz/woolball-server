using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Domain.Contracts;
using Domain.Utilities;
using StackExchange.Redis;

namespace Application.Logic;

public sealed class TextToSpeechLogic : ITextToSpeechLogic
{
    private readonly IConnectionMultiplexer _redis;
    private readonly RedisChunkBuffer<TTSResponse> _chunkBuffer;

    private static readonly ConcurrentDictionary<string, int> _nextExpected = new();

    public TextToSpeechLogic(IConnectionMultiplexer redis, RedisChunkBuffer<TTSResponse> chunkBuffer)
    {
        _redis = redis;
        _chunkBuffer = chunkBuffer;
    }

    public async Task ProcessTaskResponseAsync(TaskResponse taskResponse, TaskRequest taskRequest)
    {
        try
        {
            var requestId = taskResponse.Data.RequestId;

            TTSResponse tts = ExtractTTSResponse(taskResponse.Data.Response);
            if (tts == null || string.IsNullOrEmpty(tts.AudioBase64))
            {
                tts = new TTSResponse
                {
                    AudioBase64 = "",
                    Format = "wav",
                    SampleRate = 16000,
                };
            }

            var ttsResponseList = new List<TTSResponse> { tts };

            bool hasParent = taskRequest.PrivateArgs.TryGetValue("parent", out var parentObj);
            string responseQueueId =
                hasParent && parentObj != null ? parentObj.ToString()! : taskRequest.Id.ToString();

            bool isStream =
                taskRequest.Kwargs.TryGetValue("stream", out var streamObj)
                && bool.TryParse(streamObj?.ToString(), out var s)
                && s;

            bool isLast =
                taskRequest.PrivateArgs.TryGetValue("last", out var lastObj)
                && bool.TryParse(lastObj?.ToString(), out var l)
                && l;

            if (!hasParent)
            {
                await DispatchBatchAsync(responseQueueId, ttsResponseList, sendCompletion: true);
                return;
            }

            if (isStream)
            {
                if (
                    taskRequest.PrivateArgs.TryGetValue("order", out var ordObj)
                    && int.TryParse(ordObj?.ToString(), out var streamOrder)
                )
                {
                    // Streaming with order
                    await _chunkBuffer.AddChunkAsync(responseQueueId, streamOrder, tts, isLast);

                    int nextExpected = _nextExpected.GetOrAdd(responseQueueId, 1);
                    var (drained, newNext, isComplete) =
                        await _chunkBuffer.TryDrainStreamingAsync(responseQueueId, nextExpected);
                    _nextExpected[responseQueueId] = newNext;

                    if (drained.Count > 0)
                    {
                        await DispatchBatchAsync(
                            responseQueueId,
                            drained,
                            sendCompletion: isComplete
                        );
                    }
                    else if (isComplete)
                    {
                        await DispatchBatchAsync(
                            responseQueueId,
                            new List<TTSResponse>(),
                            sendCompletion: true
                        );
                    }

                    if (isComplete)
                    {
                        _nextExpected.TryRemove(responseQueueId, out _);
                        await _chunkBuffer.CleanupAsync(responseQueueId);
                    }
                }
                else
                {
                    // Streaming without order — auto-assign order and dispatch immediately
                    int autoOrder = await _chunkBuffer.GetNextOrderAsync(responseQueueId);
                    await _chunkBuffer.AddChunkAsync(responseQueueId, autoOrder, tts, isLast);

                    await DispatchBatchAsync(
                        responseQueueId,
                        ttsResponseList,
                        sendCompletion: isLast
                    );

                    if (isLast)
                    {
                        await _chunkBuffer.CleanupAsync(responseQueueId);
                    }
                }

                return;
            }

            // Non-streaming batch case
            int batchOrder = 1;
            if (taskRequest.PrivateArgs.TryGetValue("order", out var batchOrderObj))
            {
                if (int.TryParse(batchOrderObj?.ToString(), out int parsedOrder))
                {
                    batchOrder = parsedOrder;
                }
            }

            await _chunkBuffer.AddChunkAsync(responseQueueId, batchOrder, tts, isLast);

            if (isLast)
            {
                var allChunks = await _chunkBuffer.TryGetAllBatchAsync(responseQueueId);
                if (allChunks != null && allChunks.Count > 0)
                {
                    await DispatchBatchAsync(responseQueueId, allChunks, sendCompletion: true);
                }
            }
        }
        catch (Exception ex)
        {
            try
            {
                bool hasParent = taskRequest.PrivateArgs.TryGetValue("parent", out var parentObj);
                string responseQueueId =
                    hasParent && parentObj != null
                        ? parentObj.ToString()!
                        : taskRequest.Id.ToString();

                await _chunkBuffer.CleanupAsync(responseQueueId);

                Console.WriteLine(
                    $"[TextToSpeechLogic] Sending error response to queue result_queue_{responseQueueId}"
                );

                await DispatchBatchAsync(
                    responseQueueId,
                    new List<TTSResponse>
                    {
                        new TTSResponse
                        {
                            AudioBase64 = "",
                            Format = "wav",
                            SampleRate = 16000,
                        },
                    },
                    sendCompletion: true
                );

                Console.WriteLine(
                    $"[TextToSpeechLogic] Sent error response to prevent retries for task queue result_queue_{responseQueueId}"
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

    private async Task DispatchBatchAsync(
        string requestId,
        IEnumerable<TTSResponse> responses,
        bool sendCompletion
    )
    {
        var subscriber = _redis.GetSubscriber();
        var queueName = $"result_queue_{requestId}";

        if (responses != null && responses.Count() > 0)
        {
            var responsesList = responses.ToList();

            if (responsesList.Count > 1 && sendCompletion)
            {
                var audioChunks = responsesList
                    .Where(r => !string.IsNullOrEmpty(r.AudioBase64))
                    .Select(r => r.AudioBase64)
                    .ToList();

                if (audioChunks.Count > 0)
                {
                    try
                    {
                        string combinedAudio = AudioCombiner.CombineWavBase64(audioChunks);

                        var combinedResponse = new TTSResponse
                        {
                            AudioBase64 = combinedAudio,
                            Format = responsesList.First().Format,
                            SampleRate = responsesList.First().SampleRate,
                        };

                        responsesList = new List<TTSResponse> { combinedResponse };
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"[TextToSpeechLogic] Error combining audio: {ex.Message}"
                        );
                    }
                }
            }

            var payload = JsonSerializer.Serialize(responsesList);
            await subscriber.PublishAsync(RedisChannel.Literal(queueName), payload);
        }

        if (sendCompletion)
        {
            var completion = JsonSerializer.Serialize(new { Status = "Completed" });
            await subscriber.PublishAsync(RedisChannel.Literal(queueName), completion);
        }
    }

    private TTSResponse ExtractTTSResponse(object responseObj)
    {
        try
        {
            if (responseObj is TTSResponse ttsResponse)
            {
                return ttsResponse;
            }

            if (responseObj is JsonElement jsonElement)
            {
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
