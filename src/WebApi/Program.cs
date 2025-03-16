using System.Globalization;
using Application;
using Infrastructure;
using Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;
using Presentation;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire components.
// builder.AddServiceDefaults();
// builder.Services.AddProblemDetails();

CultureInfo culture = CultureInfo.CreateSpecificCulture("pt-BR");
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

builder
    .Services.AddLogs(builder.Configuration)
    .AddDatabase(builder.Configuration)
    .AddRepositories()
    .AddHelpers()
    .AddApplication()
    .AddQueuing(builder.Configuration, consumers: false)
    .AddOpenaiWebServices(builder.Configuration)
    .AddBocaSujaWebServices(builder.Configuration)
    .AddSpecificTimeZone()
    .AddSentry(builder.Configuration, builder.WebHost)
    .AddAppInsigths(builder.Configuration);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.AddEndPoints();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

// Configure the HTTP request pipeline.
// app.UseExceptionHandler();

app.UseHttpsRedirection();

app.Run();
