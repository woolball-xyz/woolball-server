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

    private static readonly List<string>  _dict = new List<string>
    {
        SpeechToText,
        TextToSpeech,
        Translation,
        TextGeneration,
    };

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
        { "completions", TextGeneration },
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
        if (_dict.Contains(task))
        {
            return true;
        }

        // Check if it's an alias
        if (_aliases.TryGetValue(task, out var officialTask))
        {
            return _dict.Contains(officialTask);
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

