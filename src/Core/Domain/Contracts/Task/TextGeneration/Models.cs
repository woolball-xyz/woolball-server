using System.Text.Json.Serialization;

namespace Domain.Contracts;

public class GenerationResponse
{
    [JsonPropertyName("generated_text")]
    public string GeneratedText { get; set; } = string.Empty;
}

public sealed class TextGenerationResponseData
{
    public string RequestId { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public GenerationResponse Response { get; set; }
}
