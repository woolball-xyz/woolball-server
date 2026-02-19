using StackExchange.Redis;

namespace Infrastructure.Redis;

public interface IRedisStreamPublisher
{
    Task PublishAsync(string streamKey, string message);
}

public sealed class RedisStreamPublisher : IRedisStreamPublisher
{
    private readonly IConnectionMultiplexer _redis;

    public RedisStreamPublisher(IConnectionMultiplexer redis) => _redis = redis;

    public async Task PublishAsync(string streamKey, string message)
    {
        var db = _redis.GetDatabase();
        await db.StreamAddAsync(
            streamKey,
            [new NameValueEntry("data", message)],
            maxLength: 10000,
            useApproximateMaxLength: true
        );
    }
}
