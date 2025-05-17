using System.Text.Json.Serialization;

namespace Domain.Contracts;

public class TranslationResponse
{
    [JsonPropertyName("translated_text")]
    public string TranslatedText { get; set; } = string.Empty;

    [JsonPropertyName("source_language")]
    public string SourceLanguage { get; set; } = string.Empty;

    [JsonPropertyName("target_language")]
    public string TargetLanguage { get; set; } = string.Empty;
}

public sealed class TranslationResponseData
{
    public string RequestId { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public TranslationResponse Response { get; set; }
}
