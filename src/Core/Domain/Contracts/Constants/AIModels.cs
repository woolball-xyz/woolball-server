namespace Contracts.Constants;

public class BaseModel
{
    public required string Model { get; set; }
    public required string Dtype { get; set; }
}

public static class AvailableModels
{
    public static readonly string TextGeneration = "text-generation";
    public static readonly string SpeechToText = "automatic-speech-recognition";
    public static readonly string TextToSpeech = "text-to-speech";
    public static readonly string Translation = "translation";
    
    private static readonly IReadOnlyDictionary<string, string> _dict = new Dictionary<
        string,
        string
    >
    {
        { SpeechToText, SpeechToText },
        { TextToSpeech, TextToSpeech },
        { Translation, Translation },
        { TextGeneration, TextGeneration },
    };
    public static IReadOnlyDictionary<string, string> Dict => _dict;
    
    // Dictionary for mapping aliases to official task types
    private static readonly IReadOnlyDictionary<string, string> _aliases = new Dictionary<
        string,
        string
    >(StringComparer.OrdinalIgnoreCase)
    {
        // Aliases for automatic-speech-recognition
        { "stt", SpeechToText },
        { "speech-to-text", SpeechToText },
        { "speech-recognition", SpeechToText },
        { "speech-recognize", SpeechToText },
        
        // Aliases for text-to-speech
        { "tts", TextToSpeech },
        
        // Aliases for text-generation
        { "completions", TextGeneration }
    };

    public static readonly Dictionary<string, List<string>> Names =
        new() 
        { 
            { SpeechToText, SpeechRecognitionModels.Models.Select(x => x.Model).ToList() },
            { TextGeneration, CompletionModels.Models.Select(x => x.Model).ToList() },
            { TextToSpeech, TextToSpeechModels.Models.Select(x => x.Model).ToList() },
            { Translation, TranslationModels.Models.Select(x => x.Model).ToList() }
        };

    public static string GetTaskName(string task)
    {
        // First check if this is an alias
        if (_aliases.TryGetValue(task, out var officialTask))
        {
            return officialTask;
        }
        
        return task;
    }
    
    // Helper method to check if a string is a recognized task or alias
    public static bool IsValidTask(string task)
    {
        // Check if it's a direct task
        if (Names.ContainsKey(task))
        {
            return true;
        }
        
        // Check if it's an alias
        if (_aliases.TryGetValue(task, out var officialTask))
        {
            return Names.ContainsKey(officialTask);
        }
        
        return false;
    }
}

public class CompletionModel : BaseModel { }

public class SpeechRecognitionModel : BaseModel
{
    public bool OutputLanguage { get; set; } = false;
    public bool ReturnTimestamps { get; set; } = false;
}

public class TextToSpeechModel : BaseModel 
{
    public string Voice { get; set; } = "default";
}

public class TranslationModel : BaseModel
{
    public List<string> SupportedLanguages { get; set; } = new();
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

public static class TextToSpeechModels
{
    public static readonly List<TextToSpeechModel> Models =
        new()
        {
            new TextToSpeechModel
            {
                Model = "microsoft/speecht5-tts",
                Dtype = "fp16",
                Voice = "default"
            },
        };
}

public static class TranslationModels
{
    public static readonly List<TranslationModel> Models =
        new()
        {
            new TranslationModel
            {
                Model = "facebook/nllb-200-distilled-600M",
                Dtype = "q4",
                SupportedLanguages = new List<string> 
                { 
                    "en", "es", "fr", "de", "pt", "it", "ru", "zh", "ja", "ko" 
                }
            },
        };
}
