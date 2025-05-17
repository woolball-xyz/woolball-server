using System.Globalization;
using System.Text;
using System.Threading.RateLimiting;
using Application;
using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Presentation;

var builder = WebApplication.CreateBuilder(args);

// add cors
builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "CorsPolicy",
        builder =>
        {
            builder
                .AllowAnyMethod()
                .AllowAnyHeader()
                .SetIsOriginAllowed(_ => true)
                .AllowCredentials();
        }
    );
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddRateLimiter(_ =>
    _.AddFixedWindowLimiter(
        policyName: "fixed",
        options =>
        {
            options.PermitLimit = 100;
            options.Window = TimeSpan.FromMinutes(1);
            options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            options.QueueLimit = 0;
        }
    )
);

builder.Services.AddRedis(builder.Configuration).AddApplication();

var app = builder.Build();

app.UseCors("CorsPolicy");

app.UseRateLimiter();

app.AddEndPoints();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.Run();
