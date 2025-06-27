using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Text.Json;

namespace WebApi.Filters;

/// <summary>
/// Swagger filter to configure OneOf schema for TextGeneration endpoint
/// </summary>
public class TextGenerationSchemaFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        // Create base schema with common properties
        var baseSchema = new OpenApiSchema
        {
            Type = "object",
            Properties = new Dictionary<string, OpenApiSchema>
            {
                ["provider"] = new OpenApiSchema
                {
                    Type = "string",
                    Description = "The AI provider to use for text generation",
                    Enum = new List<IOpenApiAny>
                    {
                        new OpenApiString("transformers"),
                        new OpenApiString("webllm"),
                        new OpenApiString("mediapipe")
                    }
                },
                ["model"] = new OpenApiSchema
                {
                    Type = "string",
                    Description = "The AI model to use for processing"
                },
                ["input"] = new OpenApiSchema
                {
                    Type = "string",
                    Description = "Input text or messages for generation"
                }
            },
            Required = new HashSet<string> { "provider", "model", "input" }
        };

        // Create provider-specific schemas
        var transformersSchema = CreateTransformersSchema();
        var webllmSchema = CreateWebLLMSchema();
        var mediapipeSchema = CreateMediaPipeSchema();

        // Create unified schema using AllOf
        var textGenerationRequestSchema = new OpenApiSchema
        {
            AllOf = new List<OpenApiSchema>
            {
                baseSchema,
                new OpenApiSchema
                {
                    OneOf = new List<OpenApiSchema>
                    {
                        new OpenApiSchema { Reference = new OpenApiReference { Type = ReferenceType.Schema, Id = "TransformersTextGenerationRequest" } },
                        new OpenApiSchema { Reference = new OpenApiReference { Type = ReferenceType.Schema, Id = "WebLLMTextGenerationRequest" } },
                        new OpenApiSchema { Reference = new OpenApiReference { Type = ReferenceType.Schema, Id = "MediaPipeTextGenerationRequest" } }
                    },
                    Discriminator = new OpenApiDiscriminator
                    {
                        PropertyName = "provider",
                        Mapping = new Dictionary<string, string>
                        {
                            ["transformers"] = "#/components/schemas/TransformersTextGenerationRequest",
                            ["webllm"] = "#/components/schemas/WebLLMTextGenerationRequest",
                            ["mediapipe"] = "#/components/schemas/MediaPipeTextGenerationRequest"
                        }
                    }
                }
            }
        };

        // Add schemas to components
        swaggerDoc.Components.Schemas["TextGenerationRequestContract"] = textGenerationRequestSchema;
        swaggerDoc.Components.Schemas["TransformersTextGenerationRequest"] = transformersSchema;
        swaggerDoc.Components.Schemas["WebLLMTextGenerationRequest"] = webllmSchema;
        swaggerDoc.Components.Schemas["MediaPipeTextGenerationRequest"] = mediapipeSchema;

        // Update the endpoint to use the unified schema
        UpdateTextGenerationEndpoint(swaggerDoc);
    }

    private static OpenApiSchema CreateTransformersSchema()
    {
        return new OpenApiSchema
        {
            Type = "object",
            Properties = new Dictionary<string, OpenApiSchema>
            {
                ["provider"] = new OpenApiSchema { Type = "string", Enum = new List<IOpenApiAny> { new OpenApiString("transformers") } },
                ["dtype"] = new OpenApiSchema { Type = "string", Description = "Quantization level (e.g., 'fp16', 'q4', 'q8')" },
                ["max_length"] = new OpenApiSchema { Type = "integer", Description = "Maximum length the generated tokens can have" },
                ["max_new_tokens"] = new OpenApiSchema { Type = "integer", Description = "Maximum number of tokens to generate" },
                ["min_length"] = new OpenApiSchema { Type = "integer", Description = "Minimum length of the sequence to be generated" },
                ["min_new_tokens"] = new OpenApiSchema { Type = "integer", Description = "Minimum numbers of tokens to generate" },
                ["do_sample"] = new OpenApiSchema { Type = "boolean", Description = "Whether to use sampling" },
                ["num_beams"] = new OpenApiSchema { Type = "integer", Description = "Number of beams for beam search" },
                ["no_repeat_ngram_size"] = new OpenApiSchema { Type = "integer", Description = "If > 0, all ngrams of that size can only occur once" }
            }
        };
    }

    private static OpenApiSchema CreateWebLLMSchema()
    {
        return new OpenApiSchema
        {
            Type = "object",
            Properties = new Dictionary<string, OpenApiSchema>
            {
                ["provider"] = new OpenApiSchema { Type = "string", Enum = new List<IOpenApiAny> { new OpenApiString("webllm") } },
                ["context_window_size"] = new OpenApiSchema { Type = "integer", Description = "Size of the context window for the model" },
                ["sliding_window_size"] = new OpenApiSchema { Type = "integer", Description = "Size of the sliding window for attention" },
                ["attention_sink_size"] = new OpenApiSchema { Type = "integer", Description = "Size of the attention sink" },
                ["frequency_penalty"] = new OpenApiSchema { Type = "number", Format = "double", Description = "Penalty for token frequency" },
                ["presence_penalty"] = new OpenApiSchema { Type = "number", Format = "double", Description = "Penalty for token presence" },
                ["bos_token_id"] = new OpenApiSchema { Type = "integer", Description = "Beginning of sequence token ID" }
            }
        };
    }

    private static OpenApiSchema CreateMediaPipeSchema()
    {
        return new OpenApiSchema
        {
            Type = "object",
            Properties = new Dictionary<string, OpenApiSchema>
            {
                ["provider"] = new OpenApiSchema { Type = "string", Enum = new List<IOpenApiAny> { new OpenApiString("mediapipe") } },
                ["max_tokens"] = new OpenApiSchema { Type = "integer", Description = "Maximum number of tokens to generate" },
                ["random_seed"] = new OpenApiSchema { Type = "integer", Description = "Random seed for reproducible results" }
            }
        };
    }

    private static void UpdateTextGenerationEndpoint(OpenApiDocument swaggerDoc)
    {
        foreach (var path in swaggerDoc.Paths.Values)
        {
            foreach (var operation in path.Operations.Values)
            {
                if (operation.Summary?.Contains("Text Generation") == true)
                {
                    var requestBody = operation.RequestBody;
                    if (requestBody?.Content?.ContainsKey("multipart/form-data") == true)
                    {
                        requestBody.Content["multipart/form-data"].Schema = new OpenApiSchema
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.Schema,
                                Id = "TextGenerationRequestContract"
                            }
                        };
                    }
                }
            }
        }
    }
}