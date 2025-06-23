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
using WebApi;
using WebApi.Filters;

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
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Woolball AI Network API",
        Version = "v1",
        Description = @"**Transform idle browsers into a powerful distributed AI inference network**
For detailed examples and model lists, visit our [GitHub repository](https://github.com/woolball-xyz/woolball-server).",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "Woolball Team",
            Url = new Uri("https://github.com/woolball-xyz/woolball-server")
        },
        License = new Microsoft.OpenApi.Models.OpenApiLicense
        {
            Name = "AGPL-3.0",
            Url = new Uri("https://github.com/woolball-xyz/woolball-server/blob/main/LICENSE")
        }
    });

    c.EnableAnnotations();
    c.DescribeAllParametersInCamelCase();
    
    // Enable polymorphism support for System.Text.Json with custom filters
    c.UseOneOfForPolymorphism();
    c.UseAllOfForInheritance();
    // Removed all polymorphic JSON configurations as we now use separate endpoints for each provider
    
    // Add examples for better documentation
    c.SchemaFilter<SwaggerExamplesFilter>();
    
    // Add custom document filters for oneOf schemas
    c.DocumentFilter<TextGenerationSchemaFilter>();
    c.DocumentFilter<SpeechToTextSchemaFilter>();
    
    // Group endpoints by tags based on action display name
    c.TagActionsBy(api => 
    {
        var actionName = api.ActionDescriptor.DisplayName ?? "Unknown";
        
        return actionName switch
        {
            var name when name.Contains("SpeechToText") => new[] { "Speech Recognition" },
            var name when name.Contains("TextToSpeech") => new[] { "Text-to-Speech" },
            var name when name.Contains("Translation") => new[] { "Translation" },
            var name when name.Contains("TextGeneration") => new[] { "Text Generation" },
            var name when name.Contains("Task") => new[] { "Generic" },
            var name when name.Contains("Health") => new[] { "Health" },
            _ => new[] { "AI Tasks" }
        };
    });
});

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

builder.Services
.AddRedis(builder.Configuration)  // Temporarily disabled for testing
 
 .AddApplication();

var app = builder.Build();

app.UseCors("CorsPolicy");

app.UseRateLimiter();

app.AddEndPoints();

// if (app.Environment.IsDevelopment())
// {
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Woolball AI Network API v1");
        c.RoutePrefix = "swagger";
        c.DocumentTitle = "Woolball AI Network API";
        c.DefaultModelsExpandDepth(2);
        c.DefaultModelExpandDepth(2);
        c.DisplayRequestDuration();
        c.EnableDeepLinking();
        c.EnableFilter();
        c.ShowExtensions();
        c.EnableValidator();
       
    });

// }

app.UseHttpsRedirection();

app.Run();
