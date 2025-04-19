using Infrastructure;
using Presentation;
using Presentation.Websockets;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRedis(builder.Configuration).AddWebSocketPool(builder.Configuration);

var app = builder.Build();

app.UseWebSockets();

app.AddTaskSockets(); // presentation

app.Run();
