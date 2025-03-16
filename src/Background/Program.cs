using Infrastructure;
using Presentation;

var builder = Host.CreateApplicationBuilder(args);

builder
    .Services.AddWebServices(builder.Configuration)
    .AddWorkers()
    .AddHelpers()
    .AddQueuing(builder.Configuration, consumers: true);

var host = builder.Build();

host.Run();
