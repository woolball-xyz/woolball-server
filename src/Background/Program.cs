using Infrastructure;
using Presentation;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddRedis(builder.Configuration).AddBackgroundQueues();

var host = builder.Build();

host.Run();
