using Infrastructure;
using Presentation;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddRedis(builder.Configuration).AddBackgroundQueues().AddHelpers();

// .AddQueuing(builder.Configuration, consumers: true);

var host = builder.Build();

host.Run();
