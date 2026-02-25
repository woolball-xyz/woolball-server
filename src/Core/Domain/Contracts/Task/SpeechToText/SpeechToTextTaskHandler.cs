using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

/// <summary>
/// Speech-to-text specific task handler
/// </summary>
public class SpeechToTextTaskHandler : ITaskHandler
{
    public FieldsConfig GetFieldsConfig()
    {
        return new FieldsConfig
        {
            MandatoryFields = new List<string> { "input" },
            OptionalFields = new List<string>
            {
                "model",
                "language",
                "return_timestamps",
                "stream",
                "dtype",
            },
            AllowedModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "onnx-community/whisper-large-v3-turbo_timestamped",
                "onnx-community/whisper-small",
            },
            AllowedDtypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "q4" },
        };
    }

    public async Task LoadInput(TaskRequest request)
    {
        var input = request.Kwargs["input"];
        var file = await File.ReadAllBytesAsync(input.ToString());
        request.Kwargs["input"] = file;
    }

    public Task ProcessInput(TaskRequest request)
    {
        if (!request.Kwargs.ContainsKey("input") ||
            request.Kwargs["input"] is not string input ||
            string.IsNullOrEmpty(input))
        {
            throw new InvalidOperationException("Speech-to-text requires a non-empty 'input' field");
        }

        return Task.CompletedTask;
    }
}
