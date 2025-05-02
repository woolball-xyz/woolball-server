namespace Contracts.Constants;

public class BaseModel
{
    public required string Model { get; set; }
    public required string Dtype { get; set; }
}

public static class AvailableModels
{
    public static readonly string TextGeneration = "text-generation";
    public static readonly string SpeechToText = "speech-recognition";
    public static readonly Dictionary<string, string> Names =
        new()
        {
            { SpeechToText, "automatic-speech-recognition" },
        };

    public static readonly Dictionary<string, List<string>> Names =
        new()
        {
            { SpeechToText, SpeechRecognitionModels.Models.Select(x => x.Model).ToList() },
        };

    public static string GetTaskName(string task)
    {
        if (Names.ContainsKey(task))
        {
            return Names[task];
        }
        return task;
    }
}

public class CompletionModel : BaseModel { }

public class SpeechRecognitionModel : BaseModel
{
    public bool OutputLanguage { get; set; } = false;
    public bool ReturnTimestamps { get; set; } = false;
}

public static class CompletionModels
{
    public static readonly List<CompletionModel> Models =
        new()
        {
            new CompletionModel { Model = "HuggingFaceTB/SmolLM2-135M-Instruct", Dtype = "fp16" },
            new CompletionModel { Model = "HuggingFaceTB/SmolLM2-360M-Instruct", Dtype = "q4" },
            new CompletionModel { Model = "Mozilla/Qwen2.5-0.5B-Instruct", Dtype = "q4" },
            new CompletionModel
            {
                Model = "onnx-community/Qwen2.5-Coder-0.5B-Instruct",
                Dtype = "q8",
            },
        };
}

public static class SpeechRecognitionModels
{
    public static readonly List<SpeechRecognitionModel> Models =
        new()
        {
            new SpeechRecognitionModel
            {
                Model = "onnx-community/whisper-large-v3-turbo_timestamped",
                Dtype = "q4",
                OutputLanguage = true,
                ReturnTimestamps = true,
            },
        };
}
