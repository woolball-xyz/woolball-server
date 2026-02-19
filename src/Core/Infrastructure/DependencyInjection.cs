using Infrastructure.Redis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddRedis(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        string connectionString =
            Environment.GetEnvironmentVariable("RedisConnection")
            ?? configuration.GetConnectionString("RedisConnection")
            ?? throw new KeyNotFoundException("RedisConnection");

        var options = ConfigurationOptions.Parse(connectionString);
        options.AbortOnConnectFail = false;
        options.ConnectRetry = 3;
        options.ReconnectRetryPolicy = new ExponentialRetry(5000);

        services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect(options)
        );

        services.AddSingleton<IRedisStreamPublisher, RedisStreamPublisher>();

        return services;
    }
}
