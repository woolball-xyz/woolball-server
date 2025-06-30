using System.Text.Json.Serialization;

namespace Domain.Contracts;

public class TranslationResponse
{
    [JsonPropertyName("translatedText")]
    public string TranslatedText { get; set; } = string.Empty;
}
