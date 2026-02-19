using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace WebApi.Filters;

/// <summary>
/// Swagger filter to add examples for API schemas
/// </summary>
public class SwaggerExamplesFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type == null) return;

        var typeName = context.Type.Name;

        switch (typeName)
        {
            case "SpeechToTextRequestContract":
                AddSpeechToTextExample(schema);
                break;
            case "TextToSpeechRequestContract":
                AddTextToSpeechExample(schema);
                break;
            case "TranslationRequestContract":
                AddTranslationExample(schema);
                break;
            case "TaskResponse":
                AddTaskResponseExample(schema);
                break;
        }
    }

    private static void AddSpeechToTextExample(OpenApiSchema schema)
    {
        schema.Example = new OpenApiObject
        {
            ["input"] = new OpenApiString("(audio file)"),
            ["model"] = new OpenApiString("Xenova/whisper-small"),
            ["dtype"] = new OpenApiString("q8"),
            ["language"] = new OpenApiString("en"),
            ["return_timestamps"] = new OpenApiString("true"),
            ["stream"] = new OpenApiBoolean(false)
        };
    }

    private static void AddTextToSpeechExample(OpenApiSchema schema)
    {
        schema.Example = new OpenApiObject
        {
            ["input"] = new OpenApiString("Hello, this is a test message for text-to-speech conversion."),
            ["model"] = new OpenApiString("Xenova/speecht5_tts"),
            ["speaker_embeddings"] = new OpenApiString("Xenova/speecht5_hifigan")
        };
    }

    private static void AddTranslationExample(OpenApiSchema schema)
    {
        schema.Example = new OpenApiObject
        {
            ["input"] = new OpenApiString("Hello, how are you today?"),
            ["model"] = new OpenApiString("Xenova/nllb-200-distilled-600M"),
            ["src_lang"] = new OpenApiString("eng_Latn"),
            ["tgt_lang"] = new OpenApiString("fra_Latn")
        };
    }

    private static void AddTaskResponseExample(OpenApiSchema schema)
    {
        schema.Example = new OpenApiObject
        {
            ["id"] = new OpenApiString("task_12345"),
            ["status"] = new OpenApiString("completed"),
            ["result"] = new OpenApiObject
            {
                ["generated_text"] = new OpenApiString("The future of artificial intelligence is bright and full of possibilities...")
            },
            ["created_at"] = new OpenApiString("2024-01-15T10:30:00Z"),
            ["completed_at"] = new OpenApiString("2024-01-15T10:30:05Z")
        };
    }
}