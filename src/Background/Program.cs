using Application;
using Infrastructure;
using Presentation;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddRedis(builder.Configuration).AddApplication().AddBackgroundQueues();

var host = builder.Build();

host.Run();
