using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace WebApi.Filters;

/// <summary>
/// Swagger filter to configure OneOf schema for SpeechToText input field
/// </summary>
public class SpeechToTextSchemaFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        // Create schema for the input field with oneOf
        var inputSchema = new OpenApiSchema
        {
            OneOf = new List<OpenApiSchema>
            {
                new OpenApiSchema
                {
                    Type = "string",
                    Format = "binary",
                    Description = "Audio file upload"
                },
                new OpenApiSchema
                {
                    Type = "string",
                    Format = "uri",
                    Description = "URL to audio file"
                },
                new OpenApiSchema
                {
                    Type = "string",
                    Format = "byte",
                    Description = "Base64 encoded audio data"
                }
            }
        };

        // Find the SpeechToTextRequestContract schema and update the input property
        if (swaggerDoc.Components.Schemas.TryGetValue("SpeechToTextRequestContract", out var schema))
        {
            if (schema.Properties.TryGetValue("input", out var inputProperty))
            {
                schema.Properties["input"] = inputSchema;
            }
        }

        // Update the endpoint to use the updated schema
        UpdateSpeechToTextEndpoint(swaggerDoc);
    }

    private static void UpdateSpeechToTextEndpoint(OpenApiDocument swaggerDoc)
    {
        foreach (var path in swaggerDoc.Paths.Values)
        {
            foreach (var operation in path.Operations.Values)
            {
                if (operation.Summary?.Contains("Speech Recognition") == true)
                {
                    var requestBody = operation.RequestBody;
                    if (requestBody?.Content?.ContainsKey("multipart/form-data") == true)
                    {
                        requestBody.Content["multipart/form-data"].Schema = new OpenApiSchema
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.Schema,
                                Id = "SpeechToTextRequestContract"
                            }
                        };
                    }
                }
            }
        }
    }
}