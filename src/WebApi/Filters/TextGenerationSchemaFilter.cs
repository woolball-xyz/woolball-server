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
        // Create a unified schema with all possible parameters for better codegen compatibility
        var textGenerationRequestSchema = CreateUnifiedSchema();

        // Add schema to components
        swaggerDoc.Components.Schemas["TextGenerationRequestContract"] = textGenerationRequestSchema;

        // Update the endpoint to use the unified schema
        UpdateTextGenerationEndpoint(swaggerDoc);
    }

    private static OpenApiSchema CreateUnifiedSchema()
    {
        return new OpenApiSchema
        {
            Type = "object",
            Properties = new Dictionary<string, OpenApiSchema>
            {
                // Required parameters
                ["provider"] = new OpenApiSchema 
                { 
                    Type = "string", 
                    Description = "The AI provider to use",
                    Enum = new List<IOpenApiAny> 
                    { 
                        new OpenApiString("transformers"), 
                        new OpenApiString("webllm"), 
                        new OpenApiString("mediapipe") 
                    }
                },
                ["model"] = new OpenApiSchema { Type = "string", Description = "The AI model to use for processing" },
                ["input"] = new OpenApiSchema { Type = "string", Description = "Input text or messages for generation" },
                
                // Common optional parameters
                ["top_k"] = new OpenApiSchema { Type = "integer", Description = "The number of highest probability vocabulary tokens to keep for top-k-filtering", Nullable = true },
                ["top_p"] = new OpenApiSchema { Type = "number", Format = "double", Description = "If set to float < 1, only the smallest set of most probable tokens with probabilities that add up to top_p or higher are kept for generation", Nullable = true },
                ["temperature"] = new OpenApiSchema { Type = "number", Format = "double", Description = "The value used to modulate the next token probabilities", Nullable = true },
                ["repetition_penalty"] = new OpenApiSchema { Type = "number", Format = "double", Description = "Parameter for repetition penalty. 1.0 means no penalty", Nullable = true },
                
                // Transformers-specific optional parameters
                ["dtype"] = new OpenApiSchema { Type = "string", Description = "Quantization level (e.g., 'fp16', 'q4', 'q8') - Transformers only", Nullable = true },
                ["max_length"] = new OpenApiSchema { Type = "integer", Description = "Maximum length the generated tokens can have - Transformers only", Nullable = true },
                ["max_new_tokens"] = new OpenApiSchema { Type = "integer", Description = "Maximum number of tokens to generate - Transformers only", Nullable = true },
                ["min_length"] = new OpenApiSchema { Type = "integer", Description = "Minimum length of the sequence to be generated - Transformers only", Nullable = true },
                ["min_new_tokens"] = new OpenApiSchema { Type = "integer", Description = "Minimum numbers of tokens to generate - Transformers only", Nullable = true },
                ["do_sample"] = new OpenApiSchema { Type = "boolean", Description = "Whether to use sampling - Transformers only", Nullable = true },
                ["num_beams"] = new OpenApiSchema { Type = "integer", Description = "Number of beams for beam search - Transformers only", Nullable = true },
                ["no_repeat_ngram_size"] = new OpenApiSchema { Type = "integer", Description = "If > 0, all ngrams of that size can only occur once - Transformers only", Nullable = true },
                
                // WebLLM-specific optional parameters
                ["context_window_size"] = new OpenApiSchema { Type = "integer", Description = "Size of the context window for the model - WebLLM only", Nullable = true },
                ["sliding_window_size"] = new OpenApiSchema { Type = "integer", Description = "Size of the sliding window for attention - WebLLM only", Nullable = true },
                ["attention_sink_size"] = new OpenApiSchema { Type = "integer", Description = "Size of the attention sink - WebLLM only", Nullable = true },
                ["frequency_penalty"] = new OpenApiSchema { Type = "number", Format = "double", Description = "Penalty for token frequency - WebLLM only", Nullable = true },
                ["presence_penalty"] = new OpenApiSchema { Type = "number", Format = "double", Description = "Penalty for token presence - WebLLM only", Nullable = true },
                ["bos_token_id"] = new OpenApiSchema { Type = "integer", Description = "Beginning of sequence token ID - WebLLM only", Nullable = true },
                
                // MediaPipe-specific optional parameters
                ["max_tokens"] = new OpenApiSchema { Type = "integer", Description = "Maximum number of tokens to generate - MediaPipe only", Nullable = true },
                ["random_seed"] = new OpenApiSchema { Type = "integer", Description = "Random seed for reproducible results - MediaPipe only", Nullable = true }
            },
            Required = new HashSet<string> { "provider", "model", "input" }
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