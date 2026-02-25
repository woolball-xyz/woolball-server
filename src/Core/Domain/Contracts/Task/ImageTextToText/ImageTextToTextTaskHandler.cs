using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Image-text-to-text (multimodal vision) task handler
/// </summary>
public class ImageTextToTextTaskHandler : ITaskHandler
{
    public FieldsConfig GetFieldsConfig()
    {
        return new FieldsConfig
        {
            MandatoryFields = new List<string> { "input" },
            OptionalFields = new List<string> { "model", "dtype", "max_new_tokens", "do_sample" },
            AllowedModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "HuggingFaceTB/SmolVLM-256M-Instruct",
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
        // No special input loading needed — input is already JSON/base64
        return Task.CompletedTask;
    }

    public Task ProcessInput(TaskRequest request)
    {
        if (request.Kwargs.ContainsKey("input") && request.Kwargs["input"] is string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                throw new InvalidOperationException("Input cannot be empty for image-text-to-text");
            }
        }

        return Task.CompletedTask;
    }
}
