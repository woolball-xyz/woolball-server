using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Domain.Contracts;
using StackExchange.Redis;

namespace Application.Logic
{
    public sealed class SpeechToTextLogic : ISpeechToTextLogic
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly RedisChunkBuffer<STTChunk> _chunkBuffer;

        private static readonly ConcurrentDictionary<string, int> _nextExpected = new();

        public SpeechToTextLogic(IConnectionMultiplexer redis, RedisChunkBuffer<STTChunk> chunkBuffer)
        {
            _redis = redis;
            _chunkBuffer = chunkBuffer;
        }

        public async Task ProcessTaskResponseAsync(
            TaskResponse taskResponse,
            TaskRequest taskRequest
        )
        {
            STTChunk stt;

            if (taskResponse.Data.Response is STTChunk sttr)
            {
                stt = sttr;
            }
            else
            {
                var json = JsonSerializer.Serialize(taskResponse.Data.Response);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                stt = JsonSerializer.Deserialize<STTChunk>(json, options);
            }

            if (stt == null)
                return;

            if (
                taskRequest.PrivateArgs.TryGetValue("start", out var startObj)
                && double.TryParse(startObj?.ToString(), out var baseStart)
                && stt.Chunks != null
            )
            {
                foreach (var chunk in stt.Chunks)
                {
                    chunk.Timestamp[0] += baseStart;
                    chunk.Timestamp[1] += baseStart;
                }
            }

            var sttChunksList = new List<STTChunk> { stt };

            bool hasParent = taskRequest.PrivateArgs.TryGetValue("parent", out var parentObj);
            string requestId =
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
                await DispatchBatchAsync(requestId, sttChunksList, sendCompletion: true);
                return;
            }

            if (isStream)
            {
                if (
                    taskRequest.PrivateArgs.TryGetValue("order", out var ordObj)
                    && int.TryParse(ordObj?.ToString(), out var order)
                )
                {
                    await _chunkBuffer.AddChunkAsync(requestId, order, stt, isLast);

                    int nextExpected = _nextExpected.GetOrAdd(requestId, 1);
                    var (drained, newNext, isComplete) =
                        await _chunkBuffer.TryDrainStreamingAsync(requestId, nextExpected);
                    _nextExpected[requestId] = newNext;

                    if (drained.Count > 0)
                    {
                        await DispatchBatchAsync(requestId, drained, sendCompletion: isComplete);
                    }
                    else if (isComplete)
                    {
                        await DispatchBatchAsync(
                            requestId,
                            new List<STTChunk>(),
                            sendCompletion: true
                        );
                    }

                    if (isComplete)
                    {
                        _nextExpected.TryRemove(requestId, out _);
                        await _chunkBuffer.CleanupAsync(requestId);
                    }
                }
                else
                {
                    await DispatchBatchAsync(requestId, sttChunksList, sendCompletion: isLast);
                }

                return;
            }

            // Non-streaming batch case
            if (
                taskRequest.PrivateArgs.TryGetValue("order", out var batchOrdObj)
                && int.TryParse(batchOrdObj?.ToString(), out var batchOrder)
            )
            {
                await _chunkBuffer.AddChunkAsync(requestId, batchOrder, stt, isLast);
            }
            else
            {
                await _chunkBuffer.AddChunkAsync(requestId, 1, stt, isLast);
            }

            if (isLast)
            {
                var allChunks = await _chunkBuffer.TryGetAllBatchAsync(requestId);
                if (allChunks != null)
                {
                    var sorted = allChunks
                        .OrderBy(c =>
                            c.Chunks != null && c.Chunks.Count > 0
                                ? c.Chunks[0].Timestamp[0]
                                : double.MaxValue
                        )
                        .ToList();

                    await DispatchBatchAsync(requestId, sorted, sendCompletion: true);
                }
            }
        }

        private async Task DispatchBatchAsync(
            string requestId,
            IEnumerable<STTChunk> chunks,
            bool sendCompletion
        )
        {
            var subscriber = _redis.GetSubscriber();
            var queueName = $"result_queue_{requestId}";

            if (chunks != null && chunks.Any())
            {
                var payload = JsonSerializer.Serialize(chunks);
                Console.WriteLine($"Sending chunks to {queueName}, count: {chunks.Count()}");
                await subscriber.PublishAsync(RedisChannel.Literal(queueName), payload);
            }

            if (sendCompletion)
            {
                var completion = JsonSerializer.Serialize(new { Status = "Completed" });
                Console.WriteLine($"Sending completion status to {queueName}");
                await subscriber.PublishAsync(RedisChannel.Literal(queueName), completion);
            }
        }
    }
}
