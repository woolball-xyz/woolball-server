using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Presentation.Queues;
using Presentation.Websockets;

namespace Infrastructure;

public static class WebSocketExtensions
{
    public static IServiceCollection AddWebSocketPool(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddSingleton<WebSocketNodesQueue>();
        services.AddHostedService<DistributeQueue>();

        return services;
    }
}
