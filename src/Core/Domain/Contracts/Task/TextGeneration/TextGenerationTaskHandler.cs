using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Text generation specific task handler
/// </summary>
public class TextGenerationTaskHandler : ITaskHandler
{
    public FieldsConfig GetFieldsConfig()
    {
        return new FieldsConfig
        {
            MandatoryFields = new List<string> { "input" },
            OptionalFields = new List<string> { "model" },
            AllowedModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                // Transformers.js
                "HuggingFaceTB/SmolLM2-135M-Instruct",
                "HuggingFaceTB/SmolLM2-360M-Instruct",
                "Mozilla/Qwen2.5-0.5B-Instruct",
                "onnx-community/Qwen2.5-Coder-0.5B-Instruct",
                // WebLLM
                "DeepSeek-R1-Distill-Qwen-7B-q4f16_1-MLC",
                "DeepSeek-R1-Distill-Llama-8B-q4f16_1-MLC",
                "SmolLM2-1.7B-Instruct-q4f32_1-MLC",
                "Llama-3.1-8B-Instruct-q4f32_1-MLC",
                "Qwen3-8B-q4f32_1-MLC",
                // MediaPipe
                "https://woolball.sfo3.cdn.digitaloceanspaces.com/gemma2-2b-it-cpu-int8.task",
                "https://woolball.sfo3.cdn.digitaloceanspaces.com/gemma2-2b-it-gpu-int8.bin",
                "https://woolball.sfo3.cdn.digitaloceanspaces.com/gemma3-1b-it-int4.task",
                "https://woolball.sfo3.cdn.digitaloceanspaces.com/gemma3-4b-it-int4-web.task",
            },
            AllowedDtypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "fp16",
                "q4",
                "q8",
            },
        };
    }

    public Task LoadInput(TaskRequest request)
    {
        // No special input loading needed for text generation
        return Task.CompletedTask;
    }

    public Task ProcessInput(TaskRequest request)
    {
        if (request.Kwargs.ContainsKey("input") && request.Kwargs["input"] is string prompt)
        {
            if (!TextValidation.ValidateTextContent(prompt))
            {
                throw new InvalidOperationException("Invalid prompt content for text generation");
            }
        }

        return Task.CompletedTask;
    }
}
