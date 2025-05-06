using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Domain.Contracts;
using StackExchange.Redis;

namespace Application.Logic
{
    public sealed class SpeechToTextLogic : ISpeechToTextLogic
    {
        private readonly IConnectionMultiplexer _redis;

        private static readonly ConcurrentDictionary<string, List<STTChunk>> _buffers = new();

        private class StreamBuffer
        {
            public int NextExpected { get; set; } = 1;
            public SortedDictionary<int, List<STTChunk>> Pending { get; } = new();
            public int? LastOrder { get; set; }
        }

        private static readonly ConcurrentDictionary<string, StreamBuffer> _streamBuffers = new();

        public SpeechToTextLogic(IConnectionMultiplexer redis) => _redis = redis;

        public async Task ProcessTaskResponseAsync(
            TaskResponse taskResponse,
            TaskRequest taskRequest
        )
        {
            var stt = taskResponse.Data.Response;
            Console.WriteLine(
                $"Processing task response for request : {JsonSerializer.Serialize(taskResponse)}"
            );
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
                await DispatchBatchAsync(
                    requestId,
                    sttChunksList,
                    sendCompletion: true
                );
                return;
            }

            if (isStream)
            {
                if (
                    taskRequest.PrivateArgs.TryGetValue("order", out var ordObj)
                    && int.TryParse(ordObj?.ToString(), out var order)
                )
                {
                    var buf = _streamBuffers.GetOrAdd(requestId, _ => new StreamBuffer());
                    List<STTChunk> toSend = new();
                    bool sendCompletion = false;

                    lock (buf)
                    {
                        if (isLast)
                            buf.LastOrder = order;

                        if (!buf.Pending.TryGetValue(order, out var list))
                        {
                            list = new List<STTChunk>();
                            buf.Pending[order] = list;
                        }
                        list.Add(stt);

                        while (buf.Pending.TryGetValue(buf.NextExpected, out var ready))
                        {
                            toSend.AddRange(ready);
                            buf.Pending.Remove(buf.NextExpected);
                            buf.NextExpected++;
                        }

                        if (buf.LastOrder.HasValue && buf.NextExpected > buf.LastOrder.Value)
                        {
                            sendCompletion = true;
                            _streamBuffers.TryRemove(requestId, out _);
                        }
                    }

                    if (toSend.Count > 0 || sendCompletion)
                    {
                        await DispatchBatchAsync(requestId, toSend, sendCompletion);
                    }
                }
                else
                {
                    await DispatchBatchAsync(requestId, sttChunksList, sendCompletion: isLast);
                }

                return;
            }

            var buffer = _buffers.GetOrAdd(requestId, _ => new List<STTChunk>());
            lock (buffer)
            {
                buffer.AddRange(sttChunksList);
            }

            if (isLast)
            {
                List<STTChunk> toSend;
                lock (buffer)
                {
                    toSend = buffer
                        .OrderBy(c =>
                            c.Chunks != null && c.Chunks.Count > 0
                                ? c.Chunks[0].Timestamp[0]
                                : double.MaxValue
                        )
                        .ToList();
                    buffer.Clear();
                }
                _buffers.TryRemove(requestId, out _);
                await DispatchBatchAsync(requestId, toSend, sendCompletion: true);
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
                await subscriber.PublishAsync(RedisChannel.Literal(queueName), payload);
            }

            if (sendCompletion)
            {
                var completion = JsonSerializer.Serialize(new { Status = "Completed" });
                await subscriber.PublishAsync(RedisChannel.Literal(queueName), completion);
            }
        }
    }
}
