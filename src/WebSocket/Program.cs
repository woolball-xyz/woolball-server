using Infrastructure;
using Presentation;
using Presentation.Websockets;

var builder = WebApplication.CreateBuilder(args);

builder
    .Services.AddWebServices(builder.Configuration)
    .AddQueuing(builder.Configuration, consumers: false)
    .AddSentry(builder.Configuration, builder.WebHost)
    .AddAppInsigths(builder.Configuration);

var app = builder.Build();

app.UseWebSockets();

app.TemplateSockets();

app.Run();
