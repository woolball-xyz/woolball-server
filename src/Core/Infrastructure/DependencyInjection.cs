using Domain.Interfaces.Repositories;
using Domain.Repositories;
using Domain.WebServices;
using Infrastructure.Browser;
using Infrastructure.Contexts;
using Infrastructure.FileHandling;
// using Infrastructure.Redis;
using Infrastructure.Repositories;
using Infrastructure.WebServices;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.MSSqlServer;
using StackExchange.Redis;

namespace Infrastructure;

public static class DependencyInjection
{
    // public static IServiceCollection AddRedisCache(
    //     this IServiceCollection services,
    //     IConfiguration configuration
    // )
    // {
    //     var hostAndPort =
    //         Environment.GetEnvironmentVariable("REDIS_HOST")
    //         ?? configuration["Settings:REDIS_HOST"]
    //         ?? throw new NullReferenceException("REDIS_HOST");
    //     var pass =
    //         Environment.GetEnvironmentVariable("REDIS_PASS")
    //         ?? configuration["Settings:REDIS_PASS"]
    //         ?? throw new NullReferenceException("REDIS_PASS");
    //     var user =
    //         Environment.GetEnvironmentVariable("REDIS_USER")
    //         ?? configuration["Settings:REDIS_USER"]
    //         ?? throw new NullReferenceException("REDIS_USER");
    //     var host = hostAndPort.Split(":")[0];
    //     var port = int.Parse(hostAndPort.Split(":")[1]);
    //     var options = new ConfigurationOptions
    //     {
    //         EndPoints = { { host, port } },
    //         User = user,
    //         Password = pass,
    //         Ssl = true,
    //         SslProtocols = System.Security.Authentication.SslProtocols.Tls12
    //     };
    //     services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(options));

    //     return services;
    // }

    public static IServiceCollection AddRedis(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        string connectionString =
            Environment.GetEnvironmentVariable("REDIS")
            ?? configuration.GetConnectionString("REDIS")
            ?? throw new KeyNotFoundException("REDIS");

        services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect(connectionString)
        );
        // services.AddSingleton<IRedisContext, RedisContext>();
        // services.AddSingleton<IChatRedisRepository, ChatRedisRepository>();

        return services;
    }

    public static IServiceCollection AddWebServices(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var coreapi_url =
            Environment.GetEnvironmentVariable("COREAPI_URL")
            ?? configuration["Settings:COREAPI_URL"]
            ?? throw new KeyNotFoundException("COREAPI_URL");

        services
            .AddHttpClient(
                "CoreAPI",
                httpClient =>
                {
                    httpClient.BaseAddress = new Uri(coreapi_url);
                }
            )
            .ConfigurePrimaryHttpMessageHandler(() =>
            {
                var handler = new HttpClientHandler();
                handler.ServerCertificateCustomValidationCallback = (
                    sender,
                    cert,
                    chain,
                    sslPolicyErrors
                ) => true;
                return handler;
            });

        services.AddScoped<CoreClient>();

        services.AddScoped<ITemplateWebService, TemplateWebService>();
       

        return services;
    }

    public static IServiceCollection AddDatabase(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        string connectionString =
            Environment.GetEnvironmentVariable("CONNECTIONSTRING")
            ?? configuration.GetConnectionString("CONNECTIONSTRING")
            ?? throw new KeyNotFoundException("CONNECTIONSTRING");

        services.AddDbContext<ApplicationDbContext>(options =>
            options
                .UseSqlServer(connectionString, b => b.MigrationsAssembly("Infrastructure"))
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
        );

        return services;
    }

    public static IServiceCollection AddHelpers(this IServiceCollection services)
    {
        services.AddScoped<ISaveFile, SaveFile>();
        services.AddScoped<IEmailHandling, EmailHandling>();
        return services;
    }

    public static IServiceCollection AddStreamingUtils(this IServiceCollection services)
    {
        services.AddSingleton<IVideoHandling, VideoHandlingSingleton>();
        return services;
    }

    public static IServiceCollection AddBrowserUtils(this IServiceCollection services)
    {
        services.AddScoped<IBrowserHandler, BrowserHandler>();
        return services;
    }

    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<ITemplateRepository, TemplateRepository>();
      
        return services;
    }
}
