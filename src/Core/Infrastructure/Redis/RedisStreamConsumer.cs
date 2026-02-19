using StackExchange.Redis;

namespace Infrastructure.Redis;

public sealed class RedisStreamConsumer
{
    private readonly IConnectionMultiplexer _redis;
    private readonly string _streamKey;
    private readonly string _groupName;
    private readonly string _consumerName;

    public RedisStreamConsumer(IConnectionMultiplexer redis, string streamKey)
    {
        _redis = redis;
        _streamKey = streamKey;
        _groupName = StreamNames.ConsumerGroup(streamKey);
        _consumerName = StreamNames.ConsumerName();
    }

    public async Task ConsumeAsync(Func<string, Task> processMessage, CancellationToken cancellationToken)
    {
        var db = _redis.GetDatabase();

        await EnsureGroupExistsAsync(db);
        await ClaimPendingAsync(db, processMessage);

        while (!cancellationToken.IsCancellationRequested)
        {
            var entries = await db.StreamReadGroupAsync(
                _streamKey,
                _groupName,
                _consumerName,
                ">",
                count: 10
            );

            if (entries == null || entries.Length == 0)
            {
                await Task.Delay(100, cancellationToken);
                continue;
            }

            foreach (var entry in entries)
            {
                var data = entry["data"];
                if (!data.IsNullOrEmpty)
                {
                    await processMessage(data.ToString());
                }

                await db.StreamAcknowledgeAsync(_streamKey, _groupName, entry.Id);
            }
        }
    }

    private async Task EnsureGroupExistsAsync(IDatabase db)
    {
        try
        {
            await db.StreamCreateConsumerGroupAsync(_streamKey, _groupName, "0", createStream: true);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            // Group already exists — this is fine
        }
    }

    private async Task ClaimPendingAsync(IDatabase db, Func<string, Task> processMessage)
    {
        try
        {
            var pending = await db.StreamPendingMessagesAsync(
                _streamKey,
                _groupName,
                count: 100,
                consumerName: RedisValue.Null,
                minId: "-",
                maxId: "+"
            );

            if (pending == null || pending.Length == 0)
                return;

            var messageIds = pending.Select(p => p.MessageId).ToArray();

            var claimed = await db.StreamClaimAsync(
                _streamKey,
                _groupName,
                _consumerName,
                minIdleTimeInMs: 5000,
                messageIds: messageIds
            );

            if (claimed == null)
                return;

            foreach (var entry in claimed)
            {
                var data = entry["data"];
                if (!data.IsNullOrEmpty)
                {
                    await processMessage(data.ToString());
                }

                await db.StreamAcknowledgeAsync(_streamKey, _groupName, entry.Id);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RedisStreamConsumer] Error claiming pending for {_streamKey}: {ex.Message}");
        }
    }
}
