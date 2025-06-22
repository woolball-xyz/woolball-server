using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Presentation.Models;
using Microsoft.OpenApi.Any;
using Domain.Contracts;

namespace WebApi;

public class SwaggerSchemaExampleFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type == typeof(SpeechToTextRequest))
        {
            schema.Example = new OpenApiObject
            {
                ["input"] = new OpenApiString("[Upload audio file: MP3, WAV, M4A, etc.]"),
                ["model"] = new OpenApiString("onnx-community/whisper-large-v3-turbo_timestamped"),
                ["dtype"] = new OpenApiString("q4"),
                ["language"] = new OpenApiString("en"),
                ["returnTimestamps"] = new OpenApiBoolean(true),
                ["stream"] = new OpenApiBoolean(false)
            };
        }
        else if (context.Type == typeof(TextToSpeechRequest))
        {
            schema.Example = new OpenApiObject
            {
                ["input"] = new OpenApiString("Hello, this is a test for text to speech."),
                ["model"] = new OpenApiString("Xenova/mms-tts-eng"),
                ["dtype"] = new OpenApiString("q8"),
                ["stream"] = new OpenApiBoolean(false)
            };
        }
        else if (context.Type == typeof(TranslationRequest))
        {
            schema.Example = new OpenApiObject
            {
                ["input"] = new OpenApiString("Hello, how are you today?"),
                ["model"] = new OpenApiString("Xenova/nllb-200-distilled-600M"),
                ["dtype"] = new OpenApiString("q8"),
                ["srcLang"] = new OpenApiString("eng_Latn"),
                ["tgtLang"] = new OpenApiString("por_Latn")
            };
        }
        else if (context.Type == typeof(TextGenerationTransformersRequest))
        {
            schema.Example = new OpenApiObject
            {
                ["input"] = new OpenApiString("[{\"role\":\"system\",\"content\":\"You are a helpful assistant.\"},{\"role\":\"user\",\"content\":\"What is the capital of Brazil?\"}]"),
                ["model"] = new OpenApiString("HuggingFaceTB/SmolLM2-135M-Instruct"),
                ["dtype"] = new OpenApiString("fp16"),
                ["maxNewTokens"] = new OpenApiInteger(250),
                ["temperature"] = new OpenApiDouble(0.7),
                ["doSample"] = new OpenApiBoolean(true)
            };
        }
        else if (context.Type == typeof(TextGenerationWebLLMRequest))
        {
            schema.Example = new OpenApiObject
            {
                ["input"] = new OpenApiString("[{\"role\":\"system\",\"content\":\"You are a helpful assistant.\"},{\"role\":\"user\",\"content\":\"Explain quantum computing in simple terms.\"}]"),
                ["model"] = new OpenApiString("DeepSeek-R1-Distill-Qwen-7B-q4f16_1-MLC"),
                ["provider"] = new OpenApiString("webllm"),
                ["temperature"] = new OpenApiDouble(0.7),
                ["topP"] = new OpenApiDouble(0.95)
            };
        }
        else if (context.Type == typeof(TextGenerationMediaPipeRequest))
        {
            schema.Example = new OpenApiObject
            {
                ["input"] = new OpenApiString("[{\"role\":\"system\",\"content\":\"You are a helpful assistant.\"},{\"role\":\"user\",\"content\":\"Explain quantum computing in simple terms.\"}]"),
                ["model"] = new OpenApiString("https://woolball.sfo3.cdn.digitaloceanspaces.com/gemma3-1b-it-int4.task"),
                ["provider"] = new OpenApiString("mediapipe"),
                ["maxTokens"] = new OpenApiInteger(500),
                ["temperature"] = new OpenApiDouble(0.7),
                ["topK"] = new OpenApiInteger(40),
                ["randomSeed"] = new OpenApiInteger(12345)
            };
        }
        else if (context.Type == typeof(TaskResponse))
        {
            schema.Example = new OpenApiObject
            {
                ["id"] = new OpenApiString("123e4567-e89b-12d3-a456-426614174000"),
                ["task"] = new OpenApiString("automatic-speech-recognition"),
                ["status"] = new OpenApiString("completed"),
                ["result"] = new OpenApiObject
                {
                    ["text"] = new OpenApiString("Hello, this is the transcribed text from the audio file.")
                },
                ["error"] = new OpenApiNull(),
                ["processingTimeMs"] = new OpenApiLong(1500)
            };
        }


    }


}