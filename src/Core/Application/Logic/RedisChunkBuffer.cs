using System.Text.Json;
using StackExchange.Redis;

namespace Application.Logic;

public sealed class RedisChunkBuffer<T>
{
    private readonly IConnectionMultiplexer _redis;
    private const int TtlSeconds = 600; // 10 minutes

    public RedisChunkBuffer(IConnectionMultiplexer redis) => _redis = redis;

    private static string StreamKey(string parentId) => $"chunk_stream:{parentId}";
    private static string MetaKey(string parentId) => $"chunk_meta:{parentId}";
    private static string SeqKey(string parentId) => $"chunk_seq:{parentId}";

    public async Task AddChunkAsync(string parentId, int order, T chunk, bool isLast)
    {
        var db = _redis.GetDatabase();
        var streamKey = StreamKey(parentId);
        var data = JsonSerializer.Serialize(chunk);

        await db.StreamAddAsync(
            streamKey,
            [
                new NameValueEntry("order", order.ToString()),
                new NameValueEntry("data", data),
                new NameValueEntry("is_last", isLast ? "true" : "false")
            ]
        );

        if (isLast)
        {
            await db.StringSetAsync(MetaKey(parentId), order.ToString(), TimeSpan.FromSeconds(TtlSeconds));
        }

        await db.KeyExpireAsync(streamKey, TimeSpan.FromSeconds(TtlSeconds));
    }

    public async Task<(List<T> Chunks, int NewNextExpected, bool IsComplete)> TryDrainStreamingAsync(
        string parentId, int nextExpected)
    {
        var db = _redis.GetDatabase();
        var streamKey = StreamKey(parentId);

        var entries = await db.StreamRangeAsync(streamKey, "-", "+");
        if (entries == null || entries.Length == 0)
            return (new List<T>(), nextExpected, false);

        var sorted = new SortedDictionary<int, (RedisValue entryId, T data)>();
        foreach (var entry in entries)
        {
            var orderStr = entry["order"].ToString();
            if (!int.TryParse(orderStr, out var order))
                continue;

            var dataStr = entry["data"].ToString();
            var item = JsonSerializer.Deserialize<T>(dataStr);
            if (item != null)
            {
                sorted[order] = (entry.Id, item);
            }
        }

        var drained = new List<T>();
        var toDelete = new List<RedisValue>();
        var current = nextExpected;

        while (sorted.TryGetValue(current, out var val))
        {
            drained.Add(val.data);
            toDelete.Add(val.entryId);
            current++;
        }

        if (toDelete.Count > 0)
        {
            await db.StreamDeleteAsync(streamKey, toDelete.ToArray());
        }

        var metaVal = await db.StringGetAsync(MetaKey(parentId));
        bool isComplete = false;
        if (metaVal.HasValue && int.TryParse(metaVal.ToString(), out var lastOrder))
        {
            // current is now nextExpected + drained count
            // Complete when we've drained past the lastOrder
            isComplete = current > lastOrder;
        }

        return (drained, current, isComplete);
    }

    public async Task<List<T>?> TryGetAllBatchAsync(string parentId)
    {
        var db = _redis.GetDatabase();
        var streamKey = StreamKey(parentId);

        var metaVal = await db.StringGetAsync(MetaKey(parentId));
        if (!metaVal.HasValue || !int.TryParse(metaVal.ToString(), out var lastOrder))
            return null;

        var count = await db.StreamLengthAsync(streamKey);
        if (count < lastOrder)
            return null;

        var entries = await db.StreamRangeAsync(streamKey, "-", "+");
        if (entries == null)
            return null;

        var sorted = new SortedDictionary<int, T>();
        foreach (var entry in entries)
        {
            var orderStr = entry["order"].ToString();
            if (!int.TryParse(orderStr, out var order))
                continue;

            var dataStr = entry["data"].ToString();
            var item = JsonSerializer.Deserialize<T>(dataStr);
            if (item != null)
            {
                sorted[order] = item;
            }
        }

        // Verify all chunks are present
        for (int i = 1; i <= lastOrder; i++)
        {
            if (!sorted.ContainsKey(i))
                return null;
        }

        await CleanupAsync(parentId);
        return sorted.Values.ToList();
    }

    public async Task<int> GetNextOrderAsync(string parentId)
    {
        var db = _redis.GetDatabase();
        var val = await db.StringIncrementAsync(SeqKey(parentId));
        await db.KeyExpireAsync(SeqKey(parentId), TimeSpan.FromSeconds(TtlSeconds));
        return (int)val;
    }

    public async Task CleanupAsync(string parentId)
    {
        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync([
            (RedisKey)StreamKey(parentId),
            (RedisKey)MetaKey(parentId),
            (RedisKey)SeqKey(parentId)
        ]);
    }
}
