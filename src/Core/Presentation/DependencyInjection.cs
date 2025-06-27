﻿using System.Globalization;
using Background;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Presentation.EndPoints;

namespace Presentation;

public static class DependencyInjection
{
    public static IServiceCollection AddBackgroundQueues(this IServiceCollection services)
    {
        services.AddHostedService<PreProcessingQueue>();
        services.AddHostedService<SplitAudioBySilenceQueue>();
        services.AddHostedService<SplitTextQueue>();
        services.AddHostedService<PostProcessingQueue>();
        return services;
    }

    public static IEndpointRouteBuilder AddEndPoints(this IEndpointRouteBuilder app)
    {
        app.AddTasksEndPoints();

        return app;
    }
}
