using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Text-to-speech specific task handler
/// </summary>
public class TextToSpeechTaskHandler : ITaskHandler
{
    public FieldsConfig GetFieldsConfig()
    {
        return new FieldsConfig
        {
            MandatoryFields = new List<string> { "input" },
            OptionalFields = new List<string> { "model", "voice" },
            AllowedModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Xenova/mms-tts-eng",
                "Xenova/mms-tts-spa",
                "Xenova/mms-tts-fra",
                "Xenova/mms-tts-deu",
                "Xenova/mms-tts-por",
                "Xenova/mms-tts-rus",
                "Xenova/mms-tts-ara",
                "Xenova/mms-tts-kor",
                "onnx-community/Kokoro-82M-ONNX",
                "onnx-community/Kokoro-82M-v1.0-ONNX",
            },
            AllowedDtypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "q8" },
        };
    }

    public Task LoadInput(TaskRequest request)
    {
        // No special input loading needed for TTS
        return Task.CompletedTask;
    }

    public Task ProcessInput(TaskRequest request)
    {
        if (request.Kwargs.ContainsKey("input") && request.Kwargs["input"] is string text)
        {
            if (!TextValidation.ValidateTextContent(text))
            {
                throw new InvalidOperationException("Invalid text content for text-to-speech");
            }
        }

        return Task.CompletedTask;
    }
}
