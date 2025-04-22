using System.Collections.Concurrent;
using System.Text.Json;
using Domain.Contracts;
using StackExchange.Redis;

namespace Application.Logic
{
    /// <summary>
    /// Lógica para processamento de respostas STT,
    /// implementando quatro cenários conforme specs:
    /// 1) sem parent, não stream → envia lote sem conclusão
    /// 2) sem parent, stream      → envia lote e sinaliza conclusão
    /// 3) com parent, não stream  → acumula, envia no último com conclusão
    /// 4) com parent, stream      → envia chunks e sinaliza conclusão no último
    /// </summary>
    public sealed class SpeechToTextLogic : ISpeechToTextLogic
    {
        private readonly IConnectionMultiplexer _redis;
        private static readonly ConcurrentDictionary<string, List<STTChunk>> _buffers = new();

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

            // Ajusta timestamps absolutos em cada Chunk interno
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

            // Sempre encapsula o objeto STTChunk em uma lista para dispatch
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
                isLast = true;
                // Cenário 1 & 2: sem parent
                await DispatchBatchAsync(
                    requestId,
                    sttChunksList,
                    sendCompletion: isStream && isLast
                );
                return;
            }

            if (isStream)
            {
                // Cenário 4: com parent e stream
                await DispatchBatchAsync(requestId, sttChunksList, sendCompletion: isLast);
            }
            else
            {
                // Cenário 3: com parent e não-stream
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
                        // Ordena STTChunks pelo timestamp inicial do primeiro Chunk interno
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
        }

        /// <summary>
        /// Publica um batch de STTChunk e opcionalmente sinaliza conclusão.
        /// </summary>
        private async Task DispatchBatchAsync(
            string requestId,
            IEnumerable<STTChunk> chunks,
            bool sendCompletion
        )
        {
            var subscriber = _redis.GetSubscriber();
            var queueName = $"result_queue_{requestId}";

            var payload = JsonSerializer.Serialize(chunks);
            await subscriber.PublishAsync(RedisChannel.Literal(queueName), payload);

            if (sendCompletion)
            {
                var completion = JsonSerializer.Serialize(new { Status = "Completed" });
                await subscriber.PublishAsync(RedisChannel.Literal(queueName), completion);
            }
        }
    }
}
