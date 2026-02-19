using System.Threading.RateLimiting;
using Infrastructure;
using Microsoft.AspNetCore.RateLimiting;
using Presentation;
using Presentation.Websockets;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRedis(builder.Configuration).AddWebSocketPool(builder.Configuration);

builder.Services.AddRateLimiter(_ =>
    _.AddFixedWindowLimiter(
        policyName: "ws-fixed",
        options =>
        {
            options.PermitLimit = 50;
            options.Window = TimeSpan.FromMinutes(1);
            options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            options.QueueLimit = 0;
        }
    )
);

var app = builder.Build();

app.UseHttpsRedirection();

app.UseRateLimiter();

app.UseWebSockets();

app.AddTaskSockets(); // presentation

app.Run();
