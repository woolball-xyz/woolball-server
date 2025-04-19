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

builder.Services.AddRedis(builder.Configuration).AddApplication();

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

// var privateKey =
//     Environment.GetEnvironmentVariable("SecretKey")
//     ?? builder.Configuration["JwtSettings:SecretKey"]
//     ?? throw new Exception("Private key is null");

// builder
//     .Services.AddAuthentication(x =>
//     {
//         x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
//         x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
//     })
//     .AddJwtBearer(x =>
//     {
//         x.RequireHttpsMetadata = false;
//         x.SaveToken = true;
//         x.TokenValidationParameters = new TokenValidationParameters
//         {
//             IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(privateKey)),
//             ValidateIssuer = false,
//             ValidateAudience = false,
//             ValidateLifetime = false,
//             ClockSkew = TimeSpan.Zero,
//         };
//     });

var app = builder.Build();

app.UseRateLimiter();

app.AddEndPoints();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Configure the HTTP request pipeline.
// app.UseExceptionHandler();

app.UseHttpsRedirection();

app.Run();
