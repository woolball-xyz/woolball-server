using System.Text.Json.Serialization;

namespace Domain.Contracts.Task.TextGeneration;

/// <summary>
/// Response contract for text generation tasks
/// </summary>
public class TextGenerationResponse
{
    /// <summary>
    /// The generated text content
    /// </summary>
    [JsonPropertyName("generatedText")]
    public string GeneratedText { get; set; } = string.Empty;
}
