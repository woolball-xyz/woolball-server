using System.Collections.Concurrent;
using System.Text.Json;
using Domain.Contracts;
using StackExchange.Redis;

namespace Application.Logic;

public sealed class SpeechToTextLogic(IConnectionMultiplexer redis) : ISpeechToTextLogic
{
    private static readonly ConcurrentDictionary<
        string,
        ConcurrentDictionary<string, STTChunk>
    > _orderedResponses = new();

    public async Task ProcessTaskResponseAsync(TaskResponse taskResponse, TaskRequest taskRequest)
    {
        var sttChunk = JsonSerializer.Deserialize<STTChunk>(taskResponse.Data.Response);
        if (sttChunk == null)
            return;

        bool hasParent = taskRequest.Kwargs.TryGetValue("parent", out var parentObj);
        string parent = hasParent ? parentObj?.ToString() : string.Empty;

        bool hasOrder = taskRequest.Kwargs.TryGetValue("order", out var orderObj);
        string order = hasOrder ? orderObj?.ToString() : string.Empty;

        bool isStreaming = false;
        if (taskRequest.Kwargs.TryGetValue("stream", out var streamObj))
        {
            if (streamObj is bool streamBool)
                isStreaming = streamBool;
            else if (streamObj is string streamStr)
                isStreaming = bool.TryParse(streamStr, out var result) && result;
        }

        bool isLast = false;
        if (taskRequest.Kwargs.TryGetValue("last", out var lastObj))
        {
            if (lastObj is bool lastBool)
                isLast = lastBool;
            else if (lastObj is string lastStr)
                isLast = bool.TryParse(lastStr, out var result) && result;
        }

        if (
            taskRequest.Kwargs.TryGetValue("start", out var segmentStartObj)
            && double.TryParse(segmentStartObj?.ToString(), out var segmentStart)
        )
        {
            var relativeStart = sttChunk.Timestamp[0];
            var relativeEnd = sttChunk.Timestamp[1];

            var absoluteStart = segmentStart + relativeStart;
            var absoluteEnd = segmentStart + relativeEnd;

            sttChunk.Timestamp = new List<double> { absoluteStart, absoluteEnd };
        }

        string effectiveRequestId = string.IsNullOrEmpty(parent)
            ? taskRequest.Id.ToString()
            : parent;

        if (string.IsNullOrEmpty(parent))
        {
            await DispatchResponseAsync(effectiveRequestId, sttChunk, isLast);
            return;
        }

        if (!_orderedResponses.TryGetValue(parent, out var orderDict))
        {
            orderDict = new ConcurrentDictionary<string, STTChunk>();
            _orderedResponses[parent] = orderDict;
        }

        orderDict[order] = sttChunk;

        if (isStreaming)
        {
            await TrySendOrderedChunksAsync(parent, parent);
        }
        else if (isLast)
        {
            await SendAllChunksAsync(parent, parent, true);
        }
    }

    private async Task TrySendOrderedChunksAsync(string parent, string requestId)
    {
        if (!_orderedResponses.TryGetValue(parent, out var orderDict))
            return;

        var availableOrders = orderDict
            .Keys.Select(k => int.TryParse(k, out var num) ? num : int.MaxValue)
            .OrderBy(n => n)
            .ToList();

        if (availableOrders.Count == 0)
            return;

        int expectedOrder = availableOrders[0];
        List<int> ordersToSend = new();

        foreach (var order in availableOrders)
        {
            if (order == expectedOrder)
            {
                ordersToSend.Add(order);
                expectedOrder++;
            }
            else if (order > expectedOrder)
            {
                break;
            }
        }

        foreach (var order in ordersToSend)
        {
            if (orderDict.TryRemove(order.ToString(), out var chunk))
            {
                await DispatchResponseAsync(requestId, chunk);
            }
        }
    }

    private async Task SendAllChunksAsync(
        string parent,
        string requestId,
        bool sendCompletionMessage = false
    )
    {
        if (!_orderedResponses.TryGetValue(parent, out var orderDict))
            return;

        var allOrders = orderDict
            .Keys.Select(k => (Key: k, Order: int.TryParse(k, out var num) ? num : int.MaxValue))
            .OrderBy(pair => pair.Order)
            .Select(pair => pair.Key)
            .ToList();

        foreach (var order in allOrders)
        {
            if (orderDict.TryRemove(order, out var chunk))
            {
                bool isLastChunk = sendCompletionMessage && order == allOrders.Last();
                await DispatchResponseAsync(requestId, chunk, isLastChunk);
            }
        }

        _orderedResponses.TryRemove(parent, out _);
    }

    private async Task DispatchResponseAsync(string requestId, STTChunk chunk, bool isLast = false)
    {
        var subscriber = redis.GetSubscriber();
        var queueName = $"result_queue_{requestId}";

        var serializedChunk = JsonSerializer.Serialize(chunk);
        await subscriber.PublishAsync(queueName, serializedChunk);

        if (isLast)
        {
            var completionMessage = JsonSerializer.Serialize(new { Status = "Completed" });
            await subscriber.PublishAsync(queueName, completionMessage);
        }
    }
}
