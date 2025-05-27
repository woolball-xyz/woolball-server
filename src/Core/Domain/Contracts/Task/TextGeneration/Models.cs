using System.Text.Json.Serialization;

namespace Domain.Contracts;

public class GenerationResponse
{
    [JsonPropertyName("generated_text")]
    public string GeneratedText { get; set; } = string.Empty;
}
