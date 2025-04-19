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

        services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect(connectionString)
        );

        return services;
    }
}
